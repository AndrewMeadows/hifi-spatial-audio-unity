// TODO: put license here

using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using SimpleJSON;
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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
    public Microsoft.MixedReality.WebRTC.Unity.PeerConnection PeerConnection;

    /// <summary>
    /// Uuid string in 4221 format identifying the unique connection with a Ravi server.
    /// (Also used for LocalPeerId during WebRTC signaling)
    /// </summary>
    public string SessionId {
        get {
            if (Signaler != null) {
                return Signaler.LocalPeerId;
            }
            return "";
        }
    }
/*
    /// <summary>
    /// Url to Signal service
    /// </summary>
    [Tooltip("Signalurl")]
    public string SignalUrl = "ws://localhost:8889/";
*/

    /// <summary>
    /// Desired direction of Audio Transceiver
    /// </summary>
    [Tooltip("Desired direction of Audio")]
    public Transceiver.Direction DesiredAudioDirection = Transceiver.Direction.SendReceive;

    public RaviCommandController CommandController {
        get { return _commandController; }
    }

    public RaviCommandController InputController {
        get { return _inputController; }
    }

    private RaviCommandController _commandController;
    private RaviCommandController _inputController;
    private bool _sessionStateChanged = false;

    private SessionState _sessionState = SessionState.New;
    private Microsoft.MixedReality.WebRTC.PeerConnection _realPeerConnection;

    public void Awake() {
        Debug.Log("RaviSession.Awake");
        // Connect to PeerConnection and Signaler
        //
        // If PeerConnection is non-null then we assume it has been completely configured
        // (with MediaLine, Microphone, etc) via Unity gui-clicks.  Otherwise we create
        // ALL of the necessary components ourselves.
        if (PeerConnection == null) {
            CreatePeerConnection();
            if (Signaler == null) {
                CreateSignaler();
            } else {
                Signaler.SignalStateChangedEvent += OnSignalStateChanged;
            }
        } else {
            PeerConnection.OnInitialized.AddListener(OnPeerConnectionInitialized);
            PeerConnection.OnShutdown.AddListener(OnPeerConnectionShutdown);
            if (Signaler == null) {
                CreateSignaler();
            } else {
                Signaler.SignalStateChangedEvent -= OnSignalStateChanged;
            }
        }
        if (Signaler != null && PeerConnection != null) {
            Signaler.PeerConnection = PeerConnection;
            // This fails because PeerConnection not yet initialized
            //PeerConnection.StartConnection();
        }
        _commandController = new RaviCommandController();
        _commandController.Name = "commandController";
        _commandController.DataChannelStateChangedEvent += OnCommandChannelStateChanged;
        _inputController = new RaviCommandController();
        _inputController.Name = "inputController";
    }

    private void CreatePeerConnection() {
        Debug.Log("RaviSession.CreatePeerConnection");
        // This provides an example of what components need to be created and how they
        // should be connected.  This could be done through manual clicks in the Unity GUI.
        PeerConnection = gameObject.AddComponent<Microsoft.MixedReality.WebRTC.Unity.PeerConnection>()
            as Microsoft.MixedReality.WebRTC.Unity.PeerConnection;

        PeerConnection.OnInitialized.AddListener(OnPeerConnectionInitialized);
        PeerConnection.OnShutdown.AddListener(OnPeerConnectionShutdown);

        // create the audio components
        AudioSource audioSource = gameObject.AddComponent<AudioSource>() as AudioSource;
        MicrophoneSource micSource = gameObject.AddComponent<MicrophoneSource>() as MicrophoneSource;
        AudioReceiver audioReceiver = gameObject.AddComponent<AudioReceiver>() as AudioReceiver;
        Microsoft.MixedReality.WebRTC.Unity.AudioRenderer audioRenderer =
            gameObject.AddComponent<Microsoft.MixedReality.WebRTC.Unity.AudioRenderer>()
            as Microsoft.MixedReality.WebRTC.Unity.AudioRenderer;
        MediaLine audioLine = PeerConnection.AddMediaLine(Microsoft.MixedReality.WebRTC.MediaKind.Audio);

        // connect  the audio pipes
        audioLine.Source = micSource;
        audioLine.Receiver = audioReceiver;
        audioReceiver.AudioStreamStarted.AddListener(audioRenderer.StartRendering);
        // Note: the AudioRenderer will automatically find AudioSource on gameObject
        // and will play through that.  Also, we don't bother creating an AudioListener
        // component -- we assume one exists.
    }

    private void CreateSignaler() {
        Debug.Log("RaviSession.CreateSignaler");
        Signaler = gameObject.AddComponent<RaviSignaler>() as RaviSignaler;
        //Signaler.LogVerbosity = Ravi.RaviSignaler.Verbosity.SingleEvents; // DEBUG
        Signaler.SignalStateChangedEvent += OnSignalStateChanged;
    }

    public void Start() {
        Debug.Log("RaviSession.Start");
    }

    public void Update() {
        if (_sessionStateChanged) {
            bool eventIsNull = (SessionStateChangedEvent == null);
            _sessionStateChanged = false;
            SessionStateChangedEvent?.Invoke(_sessionState);
        }
    }

    public void Open(string signalUrl) {
        Debug.Log("RaviSession.Open");
        if (_sessionState == SessionState.New || _sessionState == SessionState.Closed) {
            // Open signal socket and try to get a _realPeerConnection
            if (PeerConnection == null) {
                throw new InvalidOperationException("null RaviSession.PeerConnection");
            }
            if (Signaler == null) {
                throw new InvalidOperationException("null RaviSession.Signaler");
            }
            StartCoroutine(Connect(signalUrl));
        }
    }

    private IEnumerator Connect(string signalUrl) {
        Debug.Log("RaviSession.Connect");
        UpdateSessionState(SessionState.Connecting);
        if (Signaler.State == RaviSignaler.SignalState.New
            || Signaler.State == RaviSignaler.SignalState.Failed
            || Signaler.State == RaviSignaler.SignalState.Closed)
        {
            // Note: RaviSignaler will generate its own LocalPeerId, however if we needed
            // to assign it then we would do so here, before Signaler.Connect()
            //Signaler.LocalPeerId = Guid.NewGuid().ToString();
            Signaler.Connect(signalUrl);
        } else if (Signaler.State == RaviSignaler.SignalState.Open) {
            // TODO?: what?
        } else if (Signaler.State == RaviSignaler.SignalState.Closing) {
            // Woops, someone crossed the beams!
            UpdateSessionState(SessionState.Failed);
            yield break;
        }

        // loop until change or timeout
        // TODO: make the timeout a class property
        const int CONNECTION_TIMEOUT_SECONDS = 5; // seconds
        DateTime expiry = DateTime.Now.AddSeconds(CONNECTION_TIMEOUT_SECONDS);
        while (_sessionState == SessionState.Connecting) {
            if (expiry < DateTime.Now) {
                UpdateSessionState(SessionState.Failed);
                Debug.Log("RaviSession.Connect timed out");
                yield break;
            }
            yield return 1;
        }
        Debug.Log("RaviSession.Connect completed");
    }

    public void Close() {
        // TODO: implement this
    }

    private void UpdateSessionState(SessionState newState) {
        if (newState != _sessionState) {
            Debug.Log($"RaviSession.UpdateSessionState '{_sessionState}' --> '{newState}'");
            _sessionState = newState;
            // Just in case this happens on a side thread we remember for later
            // and invoke the event callbacks in Update() on the main thread.
            _sessionStateChanged = true;
        }
    }

    private void OnPeerConnectionInitialized() {
        // when PeerConnection is initialized we can get a handle to the actual
        // Microsoft.MixedReality.WebRTC.PeerConnection
        _realPeerConnection = PeerConnection.Peer;

        // yay! we have a _realPeerConnection
        Debug.Log("RaviSession.OnPeerConnectionInitialized");

        // connect listeners to _realPeerConnection delegates/events
        // the _realPeerConnection has the following delegates/events of interest:
        //
        // void TransceiverAdded(Transeiver t)
        // void AudioTrackAdded(RemoteAudioTrack u)
        // void AudioTrackRemoved(Transceiver t, RemoteAudioTrack u)
        // void VideoTrackAdded(RemoteVideoTrack v)
        // void VideoTrackRemoved(Transceiver t, RemoteVideoTrack v)
        // void DataChannelAdded(DataChannel c)
        // void DataChannelRemoved(Datachannel c)
        _realPeerConnection.TransceiverAdded += this.OnTransceiverAdded;
        _realPeerConnection.AudioTrackAdded += this.OnAudioTrackAdded;
        _realPeerConnection.AudioTrackRemoved += this.OnAudioTrackRemoved;
        _realPeerConnection.DataChannelAdded += this.OnDataChannelAdded;
        _realPeerConnection.DataChannelRemoved += this.OnDataChannelRemoved;
    }

    private void OnPeerConnectionShutdown() {
        Debug.Log("RaviSession.OnPeerConnectionShutdown");
        UpdateSessionState(SessionState.Disconnected);
    }

    private void OnSignalStateChanged(RaviSignaler.SignalState state) {
        Debug.Log($"RaviSession.OnSignalStateChanged signalState='{state}' sessionState='{_sessionState}'");
        /*
        switch (state) {
            case RaviSignaler.SignaSignalnecting:
                if (_sessionState != SessionState.Connecting) {
                    Debug.Log($"Unexpected: signalState='{state}' but sessionState='{_sessionState}'");
                }
                break;
            case RaviSignaler.SignalState.Open:
                //if (_sessionState != SessionState.Connecting) {
                break;
            case RaviSignaler.SignalState.Open:
                break;
            case RaviSignaler.SignalState.Open:
                break;
            case RaviSignaler.SignalState.Open:
                break;
            case RaviSignaler.SignalState.Open:
                break;
            default:
                break;
        }
        if (state == RaviSignaler.SignalState == RaviSignaler.SignalState.Open) {
            Debug.Log("woot!");
        }
        */
    }

    void OnTransceiverAdded(Transceiver t) {
        Debug.Log($"RaviSession.OnTransceiverAdded Name='{t.Name}' Kind='{t.MediaKind}' DesiredDir='{t.DesiredDirection}' negotiatedDir='{t.NegotiatedDirection}'");
        t.DirectionChanged += this.OnTransceiverDirectionChanged;
        if (t.MediaKind == Microsoft.MixedReality.WebRTC.MediaKind.Audio && t.DesiredDirection != DesiredAudioDirection) {
            // this will trigger a renegotiation
            t.DesiredDirection = DesiredAudioDirection;
        }
    }

    void OnAudioTrackAdded(RemoteAudioTrack u) {
        Debug.Log($"RaviSession.OnAudioTrackAdded Name='{u.Name}' enabled={u.Enabled} isOutputTofDevice={u.IsOutputToDevice()}");
    }

    void OnAudioTrackRemoved(Transceiver t, RemoteAudioTrack u) {
        Debug.Log($"RaviSession.OnAudioTrackRemoved Name='{u.Name}'");
    }

    void OnDataChannelAdded(DataChannel c) {
        Debug.Log($"RaviSession.OnDataChannelAdded label='{c.Label}' ordered={c.Ordered} reliable={c.Reliable}");
        if (c.Label == "ravi.command") {
            // the 'ravi.command' DataChannel is reliable
            // and is used to exchange text "command" messages
            _commandController.DataChannel = c;
        } else if (c.Label == "ravi.input") {
            // /the 'ravi.input' DataChannel is unreliable
            // and is used to upload user input to the server
            // (e.g. keystrokes and mouse input)
            _inputController.DataChannel = c;
        } else {
            Debug.Log($"RaviSession.OnDataChannelAdded failed to find controller for DataChannel.Label='{c.Label}'");
        }
    }

    void OnCommandChannelStateChanged(DataChannel.ChannelState state) {
        if (_sessionState == SessionState.Connecting
                && state == DataChannel.ChannelState.Open) {
            // the RaviSession is considered Connected when _commandController is ready
            // to accept commands
            UpdateSessionState(SessionState.Connected);
        } else if (_sessionState == SessionState.Connected
                && state == DataChannel.ChannelState.Closed) {
            UpdateSessionState(SessionState.Disconnected);
        }
    }

    void OnDataChannelRemoved(DataChannel c) {
        Debug.Log($"RaviSession.OnDataChannelRemoved label='{c.Label}'");
        if (c.Label == "ravi.command" && _sessionState == SessionState.Connected) {
            UpdateSessionState(SessionState.Disconnected);
        }
    }

    void OnTransceiverDirectionChanged(Transceiver t) {
        Debug.Log($"RaviSession.OnTransceiverDirectionChanged Name='{t.Name}' Kind='{t.MediaKind}' DesiredDir='{t.DesiredDirection}' negotiatedDir='{t.NegotiatedDirection}'");
    }

    public void SendCommand(string command, string payload) {
        if (_commandController != null) {
            _commandController.SendCommand(command, payload);
        } else {
            Debug.Log("RaviSession.SendCommand failed for null _commandController"); }
    }

    public void SendInput(string command, string payload) {
        if (_inputController != null) {
            _inputController.SendCommand(command, payload);
        } else {
            Debug.Log("RaviSession.SendInput failed for null _inputController"); }
    }
}

} // namespace Ravi
