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

public class RaviCommandController {
    public Microsoft.MixedReality.WebRTC.DataChannel DataChannel {
        get => _dataChannel;
        set {
            _dataChannel = value;
            if (_dataChannel != null) {
                _dataChannel.MessageReceived += this.MessageHandler;
            }
        }
    }

    public string Name = "RaviCommandController";
    public Action<byte[]> MessageHandler;
    private Microsoft.MixedReality.WebRTC.DataChannel _dataChannel;
}

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

    public RaviSignaler Signaler;
    public Microsoft.MixedReality.WebRTC.Unity.PeerConnection PeerConnection;
    public string SessionId;
    public string SignalUrl = "ws://localhost:8889/";
    public Transceiver.Direction DesiredAudioDirection = Transceiver.Direction.SendReceive;

    private RaviCommandController _commandController;
    private RaviCommandController _inputController;

    private SessionState _sessionState = SessionState.New;
    private Microsoft.MixedReality.WebRTC.PeerConnection _realPeerConnection;

    public void Awake() {
        Debug.Log("Step 1: RaviSession.Awake()");
        // Step 1: Connect to PeerConnection and Signaler
        //
        // If PeerConnection is non-null then we assume it has been completely configured
        // (with MediaLine, Microphone, etc) else we will do it all ourselves.  We don't
        // expect any states between (e.g. PeerConnection exists but has no MediaLine).
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
        _commandController.Name = "command";
        _commandController.MessageHandler = this.DefaultCommandHandler;

        _inputController  = new RaviCommandController();
        _inputController.Name = "input";
        _commandController.MessageHandler = this.DefaultInputHandler;
    }

    private void DefaultCommandHandler(byte[] msg) {
        /*
        Debug.Log($"DefaultCommandHandler msg.Length={msg.Length}");
        string textMsg = System.Text.Encoding.UTF8.GetString(msg);
        try {
            JSONNode obj = JSON.Parse(textMsg);
            string key = obj["c"];
            if (_handlers.ContainsKey(key)) {
                _handlers[key](obj["p"]);
            } else {
                Debug.Log($"RouteMessage failed to find handler for command='{textMsg}'");
            }
        } catch (Exception e) {
            // msg is not a JSON string
            // perhaps it is a binary message for which we have a handler
            if (msg.Length > 0) {
                // We assume the first byte represents the 'key':
                int k = msg[0];
                string longFormKey = $"0x{k:X2}";
                if (_binaryHandlers.ContainsKey(longFormKey)) {
                    _binaryHandlers[longFormKey](msg);
                } else {
                    Debug.Log($"RouteMessage no handler for input key={longFormKey} err={e.Message}");
                }
            }
        }
        */
    }

    private void DefaultInputHandler(byte[] msg) {
        Debug.Log($"DefaultCommandHandler msg.Length={msg.Length}");
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
        Debug.Log("RaviSession.Start()");
    }

    public void Update() {
    }

    public void Open() {
        Debug.Log("Step 2: RaviSession.Open");
        if (_sessionState == SessionState.New || _sessionState == SessionState.Closed) {
            // Step 2: Open signal socket and try to make a _realPeerConnection
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
            // tell Signaler what SessionId to use...
            SanityCheckSessionId();
            Signaler.SessionId = SessionId;
            Signaler.PleaseConnect(SignalUrl);
        } else if (Signaler.State == RaviSignaler.ConnectionState.Open) {
            // TODO?: what?
        } else if (Signaler.State == RaviSignaler.ConnectionState.Closing) {
            // Woops, someone crossed the beams!
            UpdateSessionState(SessionState.Failed);
            yield break;
        }

        // loop until change or timeout
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
        Debug.Log("Milestone 3: RaviSession.OnPeerConnectionInitialized");
        // Milestone 3: we have a _realPeerConnection
        // (instead of the public Unity-only wrapper 'PeerConnection')
        //
        // when PeerConnection is initialized we can get a handle to the actual
        // Microsoft.MixedReality.WebRTC.PeerConnection
        _realPeerConnection = PeerConnection.Peer;
        UpdateSessionState(SessionState.Connected);

        // Step 4: connect listeners to _realPeerConnection delegates/events
        // the _realPeerConnection has the following delegates/events of interest:
        //
        // void TransceiverAdded(Transeiver t)
        // void AudioTrackAdded(RemoteAudioTrack u)
        // void AudioTrackRemoved(Transceiver t, RemoteAudioTrack u)
        // void VideoTrackAdded(RemoteVideoTrack v)
        // void VideoTrackRemoved(Transceiver t, RemoteVideoTrack v)
        // void DataChannelAdded(DataChannel c)
        // void DataChannelRemoved(Datachannel c)
        Debug.Log("Step 4: add realPeerConnection listeners for events");
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
        Debug.Log($"Milestone 5: OnTransceiverAdded Name='{t.Name}' Kind='{t.MediaKind}' DesiredDir='{t.DesiredDirection}' negotiatedDir='{t.NegotiatedDirection}'");
        t.DirectionChanged += this.OnTransceiverDirectionChanged;
        if (t.MediaKind == Microsoft.MixedReality.WebRTC.MediaKind.Audio && t.DesiredDirection != DesiredAudioDirection) {
            // this will trigger a renegotiation
            t.DesiredDirection = DesiredAudioDirection;
        }
    }

    void OnAudioTrackAdded(RemoteAudioTrack u) {
        Debug.Log($"Milestone 5: OnAudioTrackAdded Name='{u.Name}' enabled={u.Enabled} isOutputTofDevice={u.IsOutputToDevice()}");
    }

    void OnAudioTrackRemoved(Transceiver t, RemoteAudioTrack u) {
        Debug.Log($"Milestone 5: OnAudioTrackRemoved Name='{u.Name}'");
    }

    void OnDataChannelAdded(DataChannel c) {
        Debug.Log($"Milestone 5: OnDataChannelAdded label='{c.Label}' ordered={c.Ordered} reliable={c.Reliable}");
        if (c.Label == "ravi.command") {
            _commandController.DataChannel = c;
        } else if (c.Label == "ravi.input") {
            _inputController.DataChannel = c;
        } else {
            Debug.Log($"OnDataChannelAdded failed to find CommandHandler for DataChannel.Label='{c.Label}'");
        }
    }

    void OnDataChannelRemoved(DataChannel c) {
        Debug.Log($"Milestone 5: OnDataChannelRemoved label='{c.Label}'");
    }

    void OnTransceiverDirectionChanged(Transceiver t) {
        Debug.Log($"Milestone 6: OnTransceiverDirectionChanged Name='{t.Name}' Kind='{t.MediaKind}' DesiredDir='{t.DesiredDirection}' negotiatedDir='{t.NegotiatedDirection}'");
    }
}


} // namespace Ravi
