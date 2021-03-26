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
        get {
            return _commandController;
        }
    }

    private RaviCommandController _commandController;
    private bool _sessionStateChanged = false;

    private SessionState _sessionState = SessionState.New;
    private Microsoft.MixedReality.WebRTC.PeerConnection _realPeerConnection;

    private bool _remoteAudioTrackChanged = false;
    private bool _audioPlaying = false;
    private uint _audioChannelCount = 0;
    private uint _audioSampleRate = 0;
    private int _numAudioSourceComponents = 0;

    public void Awake() {
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
        _commandController.CommandChannelStateChangedEvent += OnCommandChannelStateChanged;
    }

    private void CreatePeerConnection() {
        HiFi.LogUtil.LogUncommonEvent(this, "CreatePeerConnection");
        // This provides an example of what components need to be created and how they
        // should be connected.  This could be done through manual clicks in the Unity GUI.
        PeerConnection = gameObject.AddComponent<Microsoft.MixedReality.WebRTC.Unity.PeerConnection>()
            as Microsoft.MixedReality.WebRTC.Unity.PeerConnection;

        PeerConnection.OnInitialized.AddListener(OnPeerConnectionInitialized);
        PeerConnection.OnShutdown.AddListener(OnPeerConnectionShutdown);

        // creae some necessary components
        MicrophoneSource micSource = gameObject.AddComponent<MicrophoneSource>() as MicrophoneSource;
        AudioReceiver audioReceiver = gameObject.AddComponent<AudioReceiver>() as AudioReceiver;
        MediaLine audioLine = PeerConnection.AddMediaLine(Microsoft.MixedReality.WebRTC.MediaKind.Audio);

        // Specifying the source and receiver of the audioLine is important to do
        // before _realPeerConnection is created because their existence determines
        // the direction of the Transceiver (e.g. "SendReceive")
        audioLine.Source = micSource;
        audioLine.Receiver = audioReceiver;

        // Note: the AudioRenderer is for debugging under Unity Editor and is optional.
        // If created: it will be possible to see the volume levels of the left
        // and right signals in the Unity Editor UI.  However, it has a side-effect:
        // it will add an AudioSource component to gameObject if one does not already
        // exist.
        Microsoft.MixedReality.WebRTC.Unity.AudioRenderer audioRenderer =
            gameObject.AddComponent<Microsoft.MixedReality.WebRTC.Unity.AudioRenderer>()
            as Microsoft.MixedReality.WebRTC.Unity.AudioRenderer;
        audioReceiver.AudioStreamStarted.AddListener(audioRenderer.StartRendering);

        // measure _numAudioSourceComponents and save for later
        _numAudioSourceComponents = gameObject.GetComponents<AudioSource>().Length;
    }

    private void CreateSignaler() {
        HiFi.LogUtil.LogDebug(this, "CreateSignaler");
        Signaler = gameObject.AddComponent<RaviSignaler>() as RaviSignaler;
        //Signaler.LogVerbosity = Ravi.RaviSignaler.Verbosity.SingleEvents; // DEBUG
        Signaler.SignalStateChangedEvent += OnSignalStateChanged;
    }

    public void Start() {
    }

    public void Update() {
        if (_sessionStateChanged) {
            _sessionStateChanged = false;
            SessionStateChangedEvent?.Invoke(_sessionState);
        }
        if (_remoteAudioTrackChanged) {
            HiFi.LogUtil.LogUncommonEvent(this, "AudioSourceChanged channelCount={0} sampleRate={1}", _audioChannelCount, _audioSampleRate);
            _remoteAudioTrackChanged = false;
        }
    }

    public void Open(string signalUrl) {
        HiFi.LogUtil.LogUncommonEvent(this, "Open");
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
        HiFi.LogUtil.LogUncommonEvent(this, "Connect");
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
                HiFi.LogUtil.LogWarning(this, "Connect timed out");
                yield break;
            }
            yield return 1;
        }
        HiFi.LogUtil.LogDebug(this, "Connect completed");
    }

    public void Close() {
        // TODO: implement this
    }

    private void UpdateSessionState(SessionState newState) {
        if (newState != _sessionState) {
            HiFi.LogUtil.LogUncommonEvent(this, "UpdateSessionState {0}-->{1}",
                _sessionState, newState);
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

        _realPeerConnection.PreferredAudioCodec = "opus";
        _realPeerConnection.PreferredAudioCodecExtraParamsRemote = "maxaveragebitrate=128000;sprop-stereo=1;stereo=1";
        _realPeerConnection.PreferredAudioCodecExtraParamsLocal = "maxaveragebitrate=64000";

        // yay! we have a _realPeerConnection
        HiFi.LogUtil.LogUncommonEvent(this, "OnPeerConnectionInitialized");

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
        _realPeerConnection.AudioTrackAdded += this.OnRemoteAudioTrackAdded;
        _realPeerConnection.AudioTrackRemoved += this.OnAudioTrackRemoved;
        _realPeerConnection.DataChannelAdded += this.OnDataChannelAdded;
        _realPeerConnection.DataChannelRemoved += this.OnDataChannelRemoved;
    }

    private void OnPeerConnectionShutdown() {
        HiFi.LogUtil.LogUncommonEvent(this, "OnPeerConnectionShutdown");
        UpdateSessionState(SessionState.Disconnected);
    }

    private void OnSignalStateChanged(RaviSignaler.SignalState state) {
        HiFi.LogUtil.LogDebug(this, "OnSignalStateChanged signalState={0} sessionState={1}", state, _sessionState);
    }

    void OnTransceiverAdded(Transceiver t) {
        HiFi.LogUtil.LogUncommonEvent(this, "OnTransceiverAdded Name='{0}' kind={1} desiredDir={2} negotiatedDir={3}",
            t.Name, t.MediaKind, t.DesiredDirection, t.NegotiatedDirection);
        t.DirectionChanged += this.OnTransceiverDirectionChanged;
        if (t.MediaKind == Microsoft.MixedReality.WebRTC.MediaKind.Audio && t.DesiredDirection != DesiredAudioDirection) {
            // this will trigger a renegotiation
            t.DesiredDirection = DesiredAudioDirection;
        }
    }

    void OnRemoteAudioTrackAdded(RemoteAudioTrack u) {
        HiFi.LogUtil.LogUncommonEvent(this, "OnRemoteAudioTrackAdded Name='{0}' enabled={1} isOutputToDevice={2}",
            u.Name, u.Enabled, u.IsOutputToDevice());
        u.AudioFrameReady += HandleRemoteAudioFrame;
        if (_numAudioSourceComponents == 0) {
            // When no AudioSource exists on gameObject we can write directly to audio device
            u.OutputToDevice(true);
        }
    }

    void OnAudioTrackRemoved(Transceiver t, RemoteAudioTrack u) {
        HiFi.LogUtil.LogUncommonEvent(this, "OnAudioTrackRemoved Name='{0}'", u.Name);
        u.AudioFrameReady -= HandleRemoteAudioFrame;
    }

    private void HandleRemoteAudioFrame(AudioFrame frame) {
        uint channelCount = frame.channelCount;
        uint sampleRate = frame.sampleRate;
        if (!_audioPlaying || channelCount != _audioChannelCount || sampleRate != _audioSampleRate) {
            _audioPlaying = true;
            _audioChannelCount = channelCount;
            _audioSampleRate = sampleRate;
            _remoteAudioTrackChanged = true;
        }
    }

    void OnDataChannelAdded(DataChannel c) {
        HiFi.LogUtil.LogUncommonEvent(this, "OnDataChannelAdded label='{0}' ordered={1} reliable={2}",
            c.Label, c.Ordered, c.Reliable);
        if (c.Label == "ravi.command") {
            // the 'ravi.command' DataChannel is reliable
            // and is used to exchange text "command" messages
            _commandController.CommandChannel = c;
        } else if (c.Label == "ravi.input") {
            // /the 'ravi.input' DataChannel is unreliable
            // and is used to upload user input to the server
            // (e.g. keystrokes and mouse input)
            _commandController.InputChannel = c;
        } else {
            HiFi.LogUtil.LogError(this, "OnDataChannelAdded failed to find controller for DataChannel.Label='{0}'", c.Label);
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
        HiFi.LogUtil.LogUncommonEvent(this, "OnDataChannelRemoved label='{0}'", c.Label);
        if (c.Label == "ravi.command" && _sessionState == SessionState.Connected) {
            UpdateSessionState(SessionState.Disconnected);
        }
    }

    void OnTransceiverDirectionChanged(Transceiver t) {
        HiFi.LogUtil.LogUncommonEvent(this, "OnTransceiverDirectionChanged Name='{0}' Kind='{1}' DesiredDir='{2}' negotiatedDir='{3}'",
            t.Name, t.MediaKind, t.DesiredDirection, t.NegotiatedDirection);
    }

    public void SendCommand(string command, string payload) {
        if (_commandController != null) {
            _commandController.SendCommand(command, payload);
        } else {
            HiFi.LogUtil.LogError(this, "SendCommand failed for null _commandController"); }
    }

    public void SendInput(string message) {
        if (_commandController != null) {
            _commandController.SendInput(message);
        } else {
            HiFi.LogUtil.LogError(this, "SendInput failed for null _commandController"); }
    }
}

} // namespace Ravi
