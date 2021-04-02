// TODO: put license here

using SimpleJSON;
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Unity.WebRTC;

namespace Ravi {

/// <summary>
/// RaviSession tracks WebRTC objects, Signaler, and
/// </summary>
[AddComponentMenu("RaviSession")]
public class RaviSession : MonoBehaviour {
    public enum SessionState {
        New = 0,
        Connecting,
        Connected,
        Disconnected,
        Failed,
        Closed
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
    [Tooltip("PeerConnection")]
    public RTCPeerConnection PeerConnection;

    /// <summary>
    /// Uuid string in 4221 format identifying the unique connection with a Ravi server.
    /// This value is not authenticated: it just needs to be unique.
    /// Also used for LocalPeerId during WebRTC signaling.
    /// </summary>
    public string SessionId { get; internal set; }

    public SessionState State { get; internal set; }

    public RaviCommandController CommandController { get; internal set; }

    bool _disposeOnDestroy = false;

    public RaviSession() {
        State = SessionState.New;
        CommandController = new RaviCommandController();
        CommandController.OnOpen += OnCommandControllerOpen;
        CommandController.OnClose += OnCommandControllerClose;
    }

    public void Awake() {
        _disposeOnDestroy = RaviUtil.InitializeWebRTC();

        // create PeerConnection if we don't yet have one
        if (PeerConnection == null) {
            // we were not given a PeerConnection from external logic
            // so we create our own and external logic must reference it
            RTCConfiguration config = default;
            config.iceServers = new RTCIceServer[] {
                new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } }
            };
            Log.UncommonEvent(this, "Connect: create PeerConnection...");
            PeerConnection = new RTCPeerConnection(ref config);
        }

        // latch onto PeerConnection
        PeerConnection.OnConnectionStateChange = OnConnectionStateChange;
        PeerConnection.OnDataChannel = CommandController.OnDataChannel;
        PeerConnection.OnTrack = OnTrack;

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

    void OnCommandControllerOpen() {
        Debug.Log("RaviSession.OnCommandControllerOpen");
        if (State == SessionState.Connecting) {
            UpdateSessionState(SessionState.Connected);
        }
    }

    void OnCommandControllerClose() {
        Debug.Log("RaviSession.OnCommandControllerClose");
        if (State == SessionState.Connected) {
            UpdateSessionState(SessionState.Closed);
        }
    }

    void OnConnectionStateChange(RTCPeerConnectionState state) {
        Debug.Log($"RaviSession.OnConnectionStateChange PeerConnection.State='{state}'");
    }

    void OnTrack(RTCTrackEvent e) {
        RTCRtpTransceiver transceiver = e.Transceiver;
        MediaStreamTrack track = e.Track;
        Debug.Log($"OnTrack transceiver.dir={transceiver.Direction} enabled={track.Enabled} kind='{track.Kind}'");
    }

    void Start() {
    }

    void Update() {
    }

    void OnDestroy() {
        if (_disposeOnDestroy) {
            RaviUtil.DisposeWebRTC();
        }
    }

    public void Connect(string signalUrl) {
        Log.UncommonEvent(this, "Open");
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

    IEnumerator ConnectInternal(string signalUrl) {
        Log.UncommonEvent(this, "ConnectInternal coroutine...");
        if (Signaler.State == RaviSignaler.SignalState.New
            || Signaler.State == RaviSignaler.SignalState.Failed
            || Signaler.State == RaviSignaler.SignalState.Closed)
        {
            UpdateSessionState(SessionState.Connecting);
            bool success = Signaler.Connect(signalUrl);
            if (!success) {
                UpdateSessionState(SessionState.Failed);
                yield break;
            }
        } else {
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

    public void Close() {
        // TODO: implement this
    }

    void UpdateSessionState(SessionState newState) {
        if (newState != State) {
            Log.UncommonEvent(this, "UpdateSessionState {0}-->{1}",
                State, newState);
            State = newState;
            SessionStateChangedEvent?.Invoke(State);
        }
    }

    void OnSignalStateChanged(RaviSignaler.SignalState state) {
        Log.Debug(this, "OnSignalStateChanged signalState={0} sessionState={1}", state, State);
    }

    public void SendCommand(string command, string payload) {
        if (CommandController != null) {
            CommandController.SendCommand(command, payload);
        } else {
            Log.Error(this, "SendCommand failed for null CommandController");
        }
    }

    public void SendInput(string message) {
        if (CommandController != null) {
            CommandController.SendInput(message);
        } else {
            Log.Error(this, "SendInput failed for null CommandController");
        }
    }
}

} // namespace Ravi
