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
/// Ravi signaler over websocket
/// </summary>
[AddComponentMenu("RaviSession")]
public class RaviSession : MonoBehaviour {
    public enum SessionState {
        New = 0,
        Connecting,
        Connected,
        Completed,
        Disconnected,
        Failed,
        Closed
    }

    public delegate void OnSessionStateChangeDelegate(SessionState state);
    public OnSessionStateChangeDelegate OnStateChange;

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
    /// Unique local user identifier
    /// </summary>
    [Tooltip("Unique local user identifier")]
    public string SessionId;

    /// <summary>
    /// Url to Signal service
    /// </summary>
    [Tooltip("Signalurl")]
    public string SignalUrl = "ws://localhost:8889/";

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
                Signaler.OnConnectionStateChange = OnSignalStateChange;
            }
        } else {
            PeerConnection.OnInitialized.AddListener(OnPeerConnectionInitialized);
            PeerConnection.OnShutdown.AddListener(OnPeerConnectionShutdown);
            if (Signaler == null) {
                CreateSignaler();
            } else {
                Signaler.OnConnectionStateChange = OnSignalStateChange;
            }
        }
        if (Signaler != null && PeerConnection != null) {
            Signaler.PeerConnection = PeerConnection;
            // This fails because PeerConnection not yet initialized
            //PeerConnection.StartConnection();
        }
        _commandController = new RaviCommandController();
        _commandController.Name = "commandController";
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
        Signaler.LogVerbosity = Ravi.RaviSignaler.Verbosity.SingleEvents; // DEBUG
        Signaler.OnConnectionStateChange = OnSignalStateChange;
    }

    public void Start() {
        Debug.Log("RaviSession.Start");
    }

    public void Update() {
    }

    public void Open() {
        Debug.Log("RaviSession.Open");
        if (_sessionState == SessionState.New || _sessionState == SessionState.Closed) {
            // Open signal socket and try to get a _realPeerConnection
            if (PeerConnection == null) {
                throw new InvalidOperationException("null RaviSession.PeerConnection");
            }
            if (Signaler == null) {
                throw new InvalidOperationException("null RaviSession.Signaler");
            }
            StartCoroutine(Connect());
        }
    }

    private void SanityCheckSessionId() {
        if (string.IsNullOrEmpty(SessionId)) {
            // SessionId is expected to be a Uuid in 4221 format.
            Guid id = Guid.NewGuid();
            SessionId = id.ToString();
        } else {
            try {
                Guid id = Guid.Parse(SessionId);
            } catch (FormatException e) {
                throw new FormatException($"bad SessionId='{SessionId}' err='{e.Message}'");
            }
        }
    }

    private IEnumerator Connect() {
        Debug.Log("RaviSession.Connect");
        UpdateSessionState(SessionState.Connecting);
        if (Signaler.State == RaviSignaler.ConnectionState.New
            || Signaler.State == RaviSignaler.ConnectionState.Failed
            || Signaler.State == RaviSignaler.ConnectionState.Closed)
        {
            SanityCheckSessionId();
            Signaler.LocalPeerId = SessionId;
            Signaler.PleaseConnect(SignalUrl);
        } else if (Signaler.State == RaviSignaler.ConnectionState.Open) {
            // TODO?: what?
        } else if (Signaler.State == RaviSignaler.ConnectionState.Closing) {
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
            if (OnStateChange != null) {
                OnStateChange.Invoke(_sessionState);
            }
        }
    }

    private void OnPeerConnectionInitialized() {
        // when PeerConnection is initialized we can get a handle to the actual
        // Microsoft.MixedReality.WebRTC.PeerConnection
        _realPeerConnection = PeerConnection.Peer;
        UpdateSessionState(SessionState.Connected);

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

    private void OnSignalStateChange(RaviSignaler.ConnectionState state) {
        Debug.Log($"RaviSession.OnSignalStateChange signalState='{state}' sessionState='{_sessionState}'");
        /*
        switch (state) {
            case RaviSignaler.ConnectionState.Connecting:
                if (_sessionState != SessionState.Connecting) {
                    Debug.Log($"Unexpected: signalState='{state}' but sessionState='{_sessionState}'");
                }
                break;
            case RaviSignaler.ConnectionState.Open:
                //if (_sessionState != SessionState.Connecting) {
                break;
            case RaviSignaler.ConnectionState.Open:
                break;
            case RaviSignaler.ConnectionState.Open:
                break;
            case RaviSignaler.ConnectionState.Open:
                break;
            case RaviSignaler.ConnectionState.Open:
                break;
            default:
                break;
        }
        if (state == RaviSignaler.ConnectionState == RaviSignaler.ConnectionState.Open) {
            Debug.Log("woot!");
        }
        */
    }

    void OnTransceiverAdded(Transceiver t) {
        Debug.Log($"OnTransceiverAdded Name='{t.Name}' Kind='{t.MediaKind}' DesiredDir='{t.DesiredDirection}' negotiatedDir='{t.NegotiatedDirection}'");
        t.DirectionChanged += this.OnTransceiverDirectionChanged;
        if (t.MediaKind == Microsoft.MixedReality.WebRTC.MediaKind.Audio && t.DesiredDirection != DesiredAudioDirection) {
            // this will trigger a renegotiation
            t.DesiredDirection = DesiredAudioDirection;
        }
    }

    void OnAudioTrackAdded(RemoteAudioTrack u) {
        Debug.Log($"OnAudioTrackAdded Name='{u.Name}' enabled={u.Enabled} isOutputTofDevice={u.IsOutputToDevice()}");
    }

    void OnAudioTrackRemoved(Transceiver t, RemoteAudioTrack u) {
        Debug.Log($"OnAudioTrackRemoved Name='{u.Name}'");
    }

    void OnDataChannelAdded(DataChannel c) {
        Debug.Log($"OnDataChannelAdded label='{c.Label}' ordered={c.Ordered} reliable={c.Reliable}");
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
            Debug.Log($"OnDataChannelAdded failed to find controller for DataChannel.Label='{c.Label}'");
        }
    }

    void OnDataChannelRemoved(DataChannel c) {
        Debug.Log($"OnDataChannelRemoved label='{c.Label}'");
    }

    void OnTransceiverDirectionChanged(Transceiver t) {
        Debug.Log($"OnTransceiverDirectionChanged Name='{t.Name}' Kind='{t.MediaKind}' DesiredDir='{t.DesiredDirection}' negotiatedDir='{t.NegotiatedDirection}'");
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
