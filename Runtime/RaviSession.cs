// TODO: put license here

using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Unity.WebRTC;

namespace Ravi {

/// <summary>
/// RaviSession tracks WebRTC objects and Signaler.
/// </summary>
[AddComponentMenu("RaviSession")]
public class RaviSession : MonoBehaviour {
    public enum SessionState {
        New = 0,
        Connecting,
        Connected,
        Disconnected,
        Failed,
        Closed,
        Unavailable
    }

    public delegate void SessionStateChangedDelegate(SessionState state);
    public event SessionStateChangedDelegate SessionStateChangedEvent;

    /// <summary>
    /// Signaler for connecting to WebRTC peer
    /// </summary>
    [Tooltip("WebRTC signaler")]
    public RaviSignaler Signaler;

    /// <summary>
    /// WebRTC PeerConnection
    /// </summary>
    public RTCPeerConnection PeerConnection { get; internal set; }

    /// <summary>
    /// Uuid string in 4221 format identifying the unique connection with a Ravi server.
    /// </summary>
    /// <remarks>
    /// This value is not authenticated: it just needs to be unique.
    /// Also used for LocalPeerId during WebRTC signaling.
    /// </remarks>
    public string SessionId { get; internal set; }

    public SessionState State { get; internal set; }

    public RaviCommandController CommandController { get; internal set; }

    MediaStream _audioStream;
    List<RTCRtpSender> _rtpSenders;
    bool _disposeWebRtcOnDestroy = false;

    public RaviSession() {
        State = SessionState.New;
        CommandController = new RaviCommandController();
        CommandController.OnOpen += OnCommandControllerOpen;
        CommandController.OnClose += OnCommandControllerClose;
    }

    void Awake() {
        _disposeWebRtcOnDestroy = RaviUtil.InitializeWebRTC();
        CreatePeerConnection();

        // pick a SessionId if we don't already have one
        if (string.IsNullOrEmpty(SessionId)) {
            SessionId = Guid.NewGuid().ToString();
        } else {
            try {
                Guid id = Guid.Parse(SessionId);
            } catch (FormatException) {
                Log.Warning(this, "cowardly refusing to use SessionId='{0}'", SessionId);
                // create a fresh uuid
                SessionId = Guid.NewGuid().ToString();
                Log.UncommonEvent(this, "new SessionId='{0}'", SessionId);
            }
        }

        // create Signaler if we don't already have one
        if (Signaler == null) {
            Signaler = gameObject.AddComponent<RaviSignaler>() as RaviSignaler;
        }

        // latch onto Signaler
        Signaler.LocalPeerId = SessionId;
        Signaler.PeerConnection = PeerConnection;
        Signaler.SignalStateChangedEvent += OnSignalStateChanged;
    }

    void CreatePeerConnection() {
        Log.Debug(this, "CreatePeerConnection");
        // we were not given a PeerConnection from external logic
        // so we create our own and external logic must reference it
        RTCConfiguration config = default;
        config.iceServers = new RTCIceServer[] {
            new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } }
        };
        PeerConnection = new RTCPeerConnection(ref config);

        // latch onto PeerConnection
        PeerConnection.OnConnectionStateChange = OnConnectionStateChange;
        PeerConnection.OnDataChannel = CommandController.OnDataChannel;
        PeerConnection.OnTrack = OnTrack;

        AddAudioCaptureTracks();
    }

    void AddAudioCaptureTracks() {
        _audioStream = Audio.CaptureStream();
        _rtpSenders = new List<RTCRtpSender>();
        foreach (MediaStreamTrack track in _audioStream.GetAudioTracks()) {
            RTCRtpSender sender = PeerConnection.AddTrack(track, _audioStream);
            _rtpSenders.Add(sender);
        }

        /* TODO: set parameters on codec to get these into the sdp Answer fmtp field
         * instead of relying on RaviSignaler to hack them into place.
        PreferredAudioCodec = "opus";
        PreferredAudioCodecExtraParamsRemote = "maxaveragebitrate=128000;sprop-stereo=1;stereo=1";
        PreferredAudioCodecExtraParamsLocal = "maxaveragebitrate=64000";
         */
    }

    void RemoveAudioCaptureTracks() {
        if (_rtpSenders != null) {
            if (PeerConnection != null) {
                foreach(var sender in _rtpSenders) {
                    PeerConnection.RemoveTrack(sender);
                }
            }
            _rtpSenders.Clear();
        }
        _audioStream = null;
    }

    void OnCommandControllerOpen() {
        Log.UncommonEvent(this, "OnCommandControllerOpen");
        if (State == SessionState.Connecting) {
            UpdateSessionState(SessionState.Connected);
        }
    }

    void OnCommandControllerClose() {
        Log.UncommonEvent(this, "OnCommandControllerClose");
        if (State == SessionState.Connected) {
            UpdateSessionState(SessionState.Closed);
        }
    }

    void OnConnectionStateChange(RTCPeerConnectionState state) {
        Log.UncommonEvent(this, "OnConnectionStateChange PeerConnection.State='{0}'", state);
    }

    void OnTrack(RTCTrackEvent e) {
        // fired when something about the track changes
        RTCRtpTransceiver transceiver = e.Transceiver;
        MediaStreamTrack track = e.Track;
        Log.UncommonEvent(this, "OnTrack transceiver.dir={0} enabled={1} kind={2}", transceiver.Direction, track.Enabled, track.Kind);
    }

    void Start() {
    }

    void Update() {
    }

    void OnDestroy() {
        RemoveAudioCaptureTracks();
        if (PeerConnection != null) {
            PeerConnection.Dispose();
            PeerConnection = null;
        }
        if (_disposeWebRtcOnDestroy) {
            RaviUtil.DisposeWebRTC();
            _disposeWebRtcOnDestroy = false;
        }
    }

    /// <summary>
    /// Connect to the Ravi websocket to begin WebRTC signaling.
    /// </summary>
    /// <param name="signalUrl">
    /// ip:port of signaling websocket.
    /// </param>
    public void Connect(string signalUrl) {
        Log.UncommonEvent(this, "Open");
        if (State == SessionState.Failed || State == SessionState.Unavailable) {
            UpdateSessionState(SessionState.Closed);
        }
        if (State == SessionState.New || State == SessionState.Closed) {
            // Open signal socket and try to get a _realPeerConnection
            if (PeerConnection == null) {
                throw new InvalidOperationException("null RaviSession.PeerConnection");
            }
            if (Signaler == null) {
                throw new InvalidOperationException("null RaviSession.Signaler");
            }
            StartCoroutine(ConnectInternal(signalUrl));
        }
    }

    /// <summary>
    /// Close connection to the WebRTC Signaler and PeerConnection
    /// </summary>
    public void Close() {
        Signaler.Disconnect();
        PeerConnection.Close();
    }

    /// <summary>
    /// Send command and playload on the WebRTC "audionet.command" WebRTC DataChannel
    /// </summary>
    public void SendCommand(string command, string payload) {
        if (CommandController != null) {
            CommandController.SendCommand(command, payload);
        } else {
            Log.Error(this, "SendCommand failed for null CommandController");
        }
    }

    /// <summary>
    /// Send message on the WebRTC "audionet.input" WebRTC DataChannel
    /// </summary>
    public void SendInput(string message) {
        if (CommandController != null) {
            CommandController.SendInput(message);
        } else {
            Log.Error(this, "SendInput failed for null CommandController");
        }
    }

    IEnumerator ConnectInternal(string signalUrl) {
        Log.UncommonEvent(this, "ConnectInternal coroutine...");
        UpdateSessionState(SessionState.Connecting);
        bool success = Signaler.Connect(signalUrl);
        if (!success) {
            UpdateSessionState(SessionState.Failed);
            yield break;
        }

        // loop until change or timeout
        // TODO: make the timeout a class property
        const int CONNECTION_TIMEOUT_SECONDS = 5; // seconds
        DateTime expiry = DateTime.Now.AddSeconds(CONNECTION_TIMEOUT_SECONDS);
        while (State == SessionState.Connecting) {
            if (expiry < DateTime.Now) {
                UpdateSessionState(SessionState.Failed);
                Log.Warning(this, "Connect timed out");
                yield break;
            }
            yield return 1;
        }
        Log.Debug(this, "ConnectInternal coroutine completed");
    }

    void UpdateSessionState(SessionState newState) {
        if (newState != State) {
            Log.UncommonEvent(this, "UpdateSessionState {0}-->{1}", State, newState);
            State = newState;
            SessionStateChangedEvent?.Invoke(State);
        }
    }

    void OnSignalStateChanged(RaviSignaler.SignalState state) {
        Log.Debug(this, "OnSignalStateChanged signalState={0} sessionState={1}", state, State);
        if (state == RaviSignaler.SignalState.Unavailable) {
            UpdateSessionState(SessionState.Unavailable);
        }
    }
}

} // namespace Ravi
