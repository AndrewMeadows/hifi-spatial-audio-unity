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
        Closed = 0,
        Signaling,
        Connected,
        ConnectedWithBothDataChannels,
        Disconnected,
        Failed,
        Closing,
        Unavailable
    }

    public delegate void SessionStateChangedDelegate(SessionState state);
    public event SessionStateChangedDelegate SessionStateChangedEvent;

    public delegate void OnInboundAudioStatsDelegate(string stats);

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

    // TODO?: get rid of the CommandController abstraction and move its behavior to this class
    public RaviCommandController CommandController { get; internal set; }

    /// <summary>
    /// The microphone device name to use as input for webrtc audio track.
    /// </summary>
    public string InputAudioDeviceName {
        set {
            // we assume we've been given a valid device name
            // and don't bother to sanity-check it
            if (value != _inputDeviceName) {
                if (_audioStream == null) { 
                    // Start() has not yet been called
                    _inputDeviceName = value;
                } else {
                    Log.Warning(this, "Changing Microphone device after Start() not yet supported.");
                    // TODO: figure out how to do this
                    //ReleaseInputAudio();
                    //_inputDeviceName = value;
                    //CaptureInputAudio();
                }
            }
        }
        get {
            return _inputDeviceName;
        }
    }
    // Note: RaviSession will throw exception if _inputDeviceName
    // is not set to a valid name before it is used, however the HiFi
    // layer knows this and should default to first available mic if
    // none is specified via external logic.
    string _inputDeviceName = "unknown";

    MediaStream _audioStream;
    AudioSource _sendAudioSource;
    AudioSource _receiveAudioSource;
    AudioClip _micClip;
    AudioStreamTrack _sendTrack;
    RTCRtpSender _sender;
    RTCRtpTransceiver _transceiver;
    bool _muteInputAudio = false;

    void Awake() {
        Log.Debug(this, "Awake");
        // Note: we split the initialization code between Awake() and Start().
        // Important components that must be established early to avoid null
        // dereferences go here in Awake() while other stuff (with defaults
        // overridden via external logic immediately after construction) is
        // in Start().

        _sendAudioSource = gameObject.AddComponent<AudioSource>() as AudioSource;
        _receiveAudioSource = gameObject.AddComponent<AudioSource>() as AudioSource;

        State = SessionState.Closed;
        CommandController = new RaviCommandController();
        CommandController.OnOpen += OnCommandControllerOpen;
        CommandController.OnClose += OnCommandControllerClose;

        // create PeerConnection
        // we start with a default RtcConfiguration
        // but the Signaler expects to receive an updated config on session start
        RTCConfiguration defaultRtcConfig = default;
        defaultRtcConfig.iceServers = new RTCIceServer[] {
            new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } },
            new RTCIceServer {
                urls = new string[] { "turn:turn.highfidelity.com:3478"},
                credential = "chariot-travesty-hook",
                credentialType = RTCIceCredentialType.Password,
                username = "clouduser"
            }
        };
        PeerConnection = new RTCPeerConnection(ref defaultRtcConfig);

        // pick a SessionId if not already set
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

        // create Signaler
        if (Signaler == null) {
            Signaler = gameObject.AddComponent<RaviSignaler>() as RaviSignaler;
        }

        // latch onto Signaler
        Signaler.LocalPeerId = SessionId;
        Signaler.PeerConnection = PeerConnection;
        Signaler.SignalStateChangedEvent += OnSignalStateChanged;
    }

    void Start() {
        Log.Debug(this, "Start");
        // This initialization stuff lives in Start() with the expectation: any
        // properties needing non-default values (e.g. InputAudioDeviceName)
        // have already been set by external logic.  The motivation here is to
        // avoid unnecessary construction-deconstruction thrash when using
        // non-defaults.

        _audioStream = new MediaStream();

        CaptureInputAudio();

        // latch onto PeerConnection
        PeerConnection.OnConnectionStateChange = OnConnectionStateChange;
        PeerConnection.OnDataChannel = CommandController.OnDataChannel;
        PeerConnection.OnTrack = OnTrack;

        /* TODO?: set parameters on codec to get these into the sdp Answer fmtp field
         * instead of relying on RaviSignaler to hack them into place.
        PreferredAudioCodec = "opus";
        PreferredAudioCodecExtraParamsRemote = "maxaveragebitrate=128000;sprop-stereo=1;stereo=1";
        PreferredAudioCodecExtraParamsLocal = "maxaveragebitrate=64000";
         */
    }

    void Update() {
    }

    /// for debug purposes
    public IEnumerator DumpAllStats() {
        // Already instantiated PeerConnection as RTCPeerConnection.
        var operation = PeerConnection.GetStats();
        yield return operation;
        if (!operation.IsError) {
            var report = operation.Value;
            Debug.Log("RaviSession.DumpRTCStats:");
            foreach (RTCStats stat in report.Stats.Values) {
                Debug.Log(stat.ToJson());
            }
        }
    }

    /// for debug purposes
    public IEnumerator DumpAudioStats() {
        // Already instantiated PeerConnection as RTCPeerConnection.
        var operation = PeerConnection.GetStats();
        yield return operation;
        if (!operation.IsError) {
            var report = operation.Value;
            Debug.Log("RaviSession.DumpRTCStats:");
            foreach (RTCStats stat in report.Stats.Values) {
                string id = stat.Id;
                if (id.StartsWith("RTCInboundRTPAudioStream")
                        || id.StartsWith("RTCMediaStreamTrack_receiver")
                        || id.StartsWith("RTCOutboundRTPAudioStream")
                        || id.StartsWith("RTCRemoteInboundRtpAudioStream"))
                {
                    Debug.Log(stat.ToJson());
                }
            }
        }
    }

    /// for debug purposes
    public IEnumerator DumpReceiverStats() {
        if (_transceiver != null) {
            RTCRtpReceiver receiver = _transceiver.Receiver;
            if (receiver != null) {
                var operation = receiver.GetStats();
                yield return operation;
                if (!operation.IsError) {
                    var report = operation.Value;
                    Debug.Log("RaviSession.DumpReceiverStats:");
                    foreach (RTCStats stat in report.Stats.Values) {
                        Debug.Log(stat.ToJson());
                    }
                }
            }
        }
    }

    void OnDestroy() {
        Microphone.End(_inputDeviceName);
        _audioStream?.Dispose();
        _audioStream = null;
        _sendTrack?.Dispose();
        _sendTrack = null;
        _audioStream = null;
        Destroy(_micClip);
        _micClip = null;
        PeerConnection?.Dispose();
        PeerConnection = null;
    }

    void OnAudioReceived(AudioClip renderer) {
        Log.Debug(this, "OnAudioReceived");
        // This is only called once when the track is established.
        // We attach the clip to _receiveAudioSource and set it playing.
        _receiveAudioSource.clip = renderer;
        _receiveAudioSource.loop = true;
        _receiveAudioSource.Play();
    }

    void OnCommandControllerOpen() {
        Log.UncommonEvent(this, "OnCommandControllerOpen SessionState={0}", State);
        if (State == SessionState.Signaling || State == SessionState.Connected) {
            UpdateState(SessionState.ConnectedWithBothDataChannels);
        }
    }

    void OnCommandControllerClose() {
        Log.UncommonEvent(this, "OnCommandControllerClose SessionState={0}", State);
        switch(State) {
            case SessionState.Signaling:
            case SessionState.Connected:
            case SessionState.ConnectedWithBothDataChannels:
                UpdateState(SessionState.Failed);
                break;
            default:
                break;
        }
    }

    void OnConnectionStateChange(RTCPeerConnectionState state) {
        Log.UncommonEvent(this, "OnConnectionStateChange PeerConnection.State='{0}'", state);
        SessionState new_state = State;
        switch(state) {
            case RTCPeerConnectionState.Connected:
                if (State == SessionState.Signaling
                        || State == SessionState.Disconnected
                        || State == SessionState.ConnectedWithBothDataChannels)
                {
                    new_state = SessionState.Connected;
                }
                if (new_state == SessionState.Connected
                        && CommandController.IsOpen())
                {
                    new_state = SessionState.ConnectedWithBothDataChannels;
                }
                break;
            case RTCPeerConnectionState.Disconnected:
                if (State == SessionState.Signaling
                        || State == SessionState.Connected
                        || State == SessionState.ConnectedWithBothDataChannels)
                {
                    new_state = SessionState.Disconnected;
                }
                break;
            case RTCPeerConnectionState.Failed:
                if (State == SessionState.Signaling
                        || State == SessionState.Connected
                        || State == SessionState.ConnectedWithBothDataChannels)
                {
                    new_state = SessionState.Failed;
                }
                break;
            case RTCPeerConnectionState.Closed:
                if (State == SessionState.Signaling
                        || State == SessionState.Connected
                        || State == SessionState.ConnectedWithBothDataChannels
                        || State == SessionState.Disconnected)
                {
                    new_state = SessionState.Failed;
                } else if (State == SessionState.Closing) {
                    new_state = SessionState.Closed;
                }
                break;
            default:
                break;
        }
        UpdateState(new_state);
    }

    void OnTrack(RTCTrackEvent e) {
        // fired when something about a track changes
        var track = e.Track as AudioStreamTrack;
        RTCRtpTransceiverDirection direction = RTCRtpTransceiverDirection.Inactive;
        RTCRtpTransceiver transceiver = e.Transceiver;
        if (_transceiver == null) {
            _transceiver = e.Transceiver;
            track.OnAudioReceived += OnAudioReceived;
        }
        if (_transceiver != null) {
            direction = _transceiver.Direction;
        }
        Log.UncommonEvent(this, "OnTrack transceiver.dir={0} enabled={1} kind={2}", direction, track.Enabled, track.Kind);
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
            UpdateState(SessionState.Closed);
        }
        if (State == SessionState.Closed) {
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
        Log.UncommonEvent(this, "Close");
        UpdateState(SessionState.Closing);
        RemoveAudioCaptureTrack();
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

    public bool InputAudioMuted {
        set {
            _muteInputAudio = value;
            if (_sendTrack != null) {
                _sendTrack.Enabled = !_muteInputAudio;
            }
        }
        get { return _muteInputAudio; }
    }

    IEnumerator ConnectInternal(string signalUrl) {
        Log.UncommonEvent(this, "ConnectInternal coroutine...");
        UpdateState(SessionState.Signaling);
        bool success = Signaler.Connect(signalUrl);
        if (!success) {
            UpdateState(SessionState.Failed);
            yield break;
        }

        // loop until change or timeout
        // TODO: make the timeout a class property
        const int CONNECTION_TIMEOUT_SECONDS = 5; // seconds
        long now = DateTimeOffset.Now.Ticks;
        const long TICKS_PER_SECOND = 10000000; // 1 tick = 100 nsec
        long expiry = now + CONNECTION_TIMEOUT_SECONDS * TICKS_PER_SECOND;
        while (State == SessionState.Signaling) {
            now = DateTimeOffset.Now.Ticks;
            if (expiry < now) {
                Log.Warning(this, "Connect timed out");
                UpdateState(SessionState.Failed);
                yield break;
            }
            yield return 1;
        }
        Log.Debug(this, "ConnectInternal coroutine completed");
    }

    void UpdateState(SessionState newState) {
        if (newState != State) {
            Log.UncommonEvent(this, "UpdateState {0}-->{1}", State, newState);
            State = newState;
            SessionStateChangedEvent?.Invoke(State);
        }
    }

    void OnSignalStateChanged(RaviSignaler.SignalState state) {
        Log.Debug(this, "OnSignalStateChanged signalState={0} sessionState={1}", state, State);
        if (state == RaviSignaler.SignalState.Unavailable) {
            UpdateState(SessionState.Unavailable);
        }
    }

    void CaptureInputAudio() {
        Log.UncommonEvent(this, "CaptureInputAudio mic='{0}' mute={1}", _inputDeviceName, _muteInputAudio);
        if (_micClip != null) {
            return;
        }
        _micClip = Microphone.Start(_inputDeviceName, true, 1, 48000);
        if (_micClip == null) {
            Log.Warning(this, "CaptureInputAudio failed to start microphone='{}'", _inputDeviceName);
        } else {
            // set the latency to “0” samples before the audio starts to play.
            while (!(Microphone.GetPosition(_inputDeviceName) > 0)) {}

            // This is how we feed audio to webrtc: we create an AudioSource,
            // loop it on _micClip, and set it playing.
            _sendAudioSource.clip = _micClip;
            _sendAudioSource.loop = true;
            _sendAudioSource.Play();

            // we mute _sendAudioSource to prevent it from echoing sound into
            // the world (e.g. audio data still reaches the webrtc stream but
            // not the world)
            _sendAudioSource.mute = true;
        }

        // add audio track
        _sendTrack = new AudioStreamTrack(_sendAudioSource);
        _sender = PeerConnection.AddTrack(_sendTrack, _audioStream);
        _sendTrack.Enabled = !_muteInputAudio;
    }

    void RemoveAudioCaptureTrack() {
        Log.UncommonEvent(this, "RemoveAudioCaptureTrack");
        if (_sendTrack != null) {
            _sendTrack.Enabled = false;
        }
        if (_sender != null) {
            PeerConnection.RemoveTrack(_sender);
            _sender.Dispose();
            _sender = null;
        }
        _sendTrack?.Dispose();
        _sendTrack = null;
    }

    void ReleaseInputAudio() {
        // DO NOT USE: WIP: this does not work as intended yet.
        // The goal it to help the RaviSession switch microphone devices but
        // removing the track triggers a NegotiationNeeded webrtc event.
        // TODO: figure out how to do this
        Log.UncommonEvent(this, "ReleaseInputAudio");
        _sendAudioSource.Stop();
        RemoveAudioCaptureTrack();
        _sendAudioSource.clip = null;
        if (_micClip) {
            Destroy(_micClip);
            _micClip = null;
        }
    }

    public IEnumerator GetInboundAudioStats(OnInboundAudioStatsDelegate handler, float period) {
        while (PeerConnection != null) {
            var operation = PeerConnection.GetStats();
            yield return operation;
            if (!operation.IsError && handler != null) {
                var report = operation.Value;
                foreach (RTCStats stat in report.Stats.Values) {
                    if (stat.Id.StartsWith("RTCInboundRTPAudioStream")) {
                        handler(stat.ToJson());
                        break;
                    }
                }
            }
            yield return new WaitForSeconds(period);
        }
    }
}

} // namespace Ravi
