/**
 * The [[HiFiSession]] component provides the main API for using the High Fidelity Audio Service
 * - `connectToHiFiAudioAPIServer()`: Connect to High Fidelity Audio Server
 * - `disconnectFromHiFiAudioAPIServer()`: Disconnect from High Fidelity Audio Server
 * - `updateUserDataAndTransmit()`: Update the user's data (position, orientation, audio parameters) on the High Fidelity Audio Server
 * - `setInputAudioMediaStream()`: Set a new input audio media stream (for example, when the user's audio input device changes)
 * @packageDocumentation
 */

using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;

namespace HiFi {

public struct HiFiMixerInfo {
    public string buildNumber;
    public string buildType;
    public string buildVersion;
    public string visitId;
    public string visitIdHash;
}

/// <summary>
/// Component for access to HiFi Spatial Audio API
/// </summary>
[AddComponentMenu("HiFi Spatial Audio Session")]
[Serializable]
public class HiFiSession : MonoBehaviour {
    // possible states of the session
    public enum AudionetConnectionState {
        Connecting = 0,
        Connected,
        Disconnected,
        Failed,
        Unavailable // e.g. when API server is at capacity
    }

    // possible scopes allowed for UserData subcription
    public enum UserDataScope {
        None = 0, // no UserData whatsoever
        Peers, // UserData about peers, but not this User
        All // UserData about peers and this User
    }

    static string[] UserDataScopeStrings = { "none", "peers", "all" };

    /// <summary>
    /// State of connection to HiFi Spatial Audio Service
    /// </summary>
    public AudionetConnectionState ConnectionState {
        get { return _connectionState; }
    }

    /// <summary>
    /// Scope of available UserData streamed from Service
    /// </summary>
    public UserDataScope UserDataStreamingScope = UserDataScope.All;


    /// <summary>
    /// Signaling service
    /// </summary>
    [Tooltip("SignalUrl")]
    public string SignalingServiceUrl = HiFi.Constants.HIFI_API_SIGNALING_URL;

    /// <summary>
    /// Json Web Token
    /// </summary>
    [Tooltip("Json Web Token (identifies client)")]
    public string JWT;

    /// <summary>
    /// RaviSession component
    /// </summary>
    [Tooltip("RaviSession")]
    public Ravi.RaviSession RaviSession;

    public delegate void ConnectionStateChangedDelegate(AudionetConnectionState state);
    public delegate void OnPeerDataUpdatedDelegate(List<IncomingAudioAPIData> peers);
    public delegate void OnPeerDisconnectedDelegate(SortedSet<string> ids);

    /// <summary>
    /// This event fires on the main thread after this HiFiSession's ConnectionState changes.
    /// </summary>
    public event ConnectionStateChangedDelegate ConnectionStateChangedEvent;

    /// <summary>
    /// This event fires on the main thread after IncomingAudioAPIData has arrived from
    /// the HiFi spatial audio server.
    /// The argument 'peers' is a copy of all the full current values for IncomingAudioAPIData
    /// changed since the last frame.
    /// </summary>
    public event OnPeerDataUpdatedDelegate PeerDataUpdatedEvent;

    /// <summary>
    /// This event fires on the main thread after a peer has been disconnected.
    /// The argument 'ids' is a SortedSet<string> of all IncomingAudioAPIData._hashedVisitId
    /// reported as disconnected since the last frame.
    /// </summary>
    public event OnPeerDisconnectedDelegate PeerDisconnectedEvent;

    public Vector3 Position {
        set {
            _userData._position = value;
            _userDataHasChanged = true;
        }
        get { return _userData._position; }
    }

    public Quaternion Orientation {
        set {
            _userData._orientation = value;
            _userDataHasChanged = true;
        }
        get { return _userData._orientation; }
    }

    public float NoiseThreshold {
        set {
            _userData._volumeThreshold = value;
            _userDataHasChanged = true;
        }
        get { return _userData._volumeThreshold; }
    }

    public float Gain {
        set {
            _userData._hiFiGain = value;
            _userDataHasChanged = true;
        }
        get { return _userData._hiFiGain; }
    }

    public float Attenuation {
        set {
            _userData._userAttenuation = value;
            _userDataHasChanged = true;
        }
        get { return _userData._userAttenuation; }
    }

    public float Rolloff {
        set {
            _userData._userRolloff = value;
            _userDataHasChanged = true;
        }
        get { return _userData._userRolloff; }
    }

    public bool InputAudioIsStereo = false;

    private AudionetConnectionState _connectionState = AudionetConnectionState.Disconnected;
    private HiFiMixerInfo _mixerInfo;
    private Dictionary<string, IncomingAudioAPIData> _peerDataMap;
    private Dictionary<string, string> _peerKeyMap; // hashdVisitId-->"peer key"
    private SortedSet<string> _changedPeerKeys;
    private SortedSet<string> _deletedVisitIds;

    private OutgoingAudioAPIData _userData;
    private OutgoingAudioAPIData _lastUserData;

    private bool _stateHasChanged = false;
    private bool _userDataHasChanged = false;
    private bool _peerDataHasChanged = false;
    private bool _peersHaveDisconnected = false;

    private void Awake() {
        if (RaviSession == null) {
            CreateRaviSession();
        }
        RaviSession.SessionStateChangedEvent += OnRaviSessionStateChanged;

        _userData = new OutgoingAudioAPIData();
        _lastUserData = new OutgoingAudioAPIData();
        _peerDataMap = new Dictionary<string, IncomingAudioAPIData>();
        _peerKeyMap = new Dictionary<string, string>();
        _changedPeerKeys = new SortedSet<string>();
        _deletedVisitIds = new SortedSet<string>();

        // By default disable most logging.
        // This can be overrdden for debuggin by external code after this hard-coded setting.
        LogUtil.GlobalMaxLogLevel = LogUtil.LogLevel.UncommonEvent;
        //LogUtil.GlobalMaxLogLevel = LogUtil.LogLevel.Debug;
    }

    private void Start() {
    }

    private void Update() {
        if (_stateHasChanged) {
            _stateHasChanged = false;
            ConnectionStateChangedEvent?.Invoke(_connectionState);
        }
        if (_connectionState == AudionetConnectionState.Connected && _userDataHasChanged) {
            // TODO: add a way to rate-limit SendUserData()
            _userDataHasChanged = false;
            SendUserData();
        }
        if (_peerDataHasChanged) {
            // it is OK to reset _peerDataHasChanged outside of the lock
            // because the handling of changes is fault-tolerante to the empty set.
            _peerDataHasChanged = false;
            if (PeerDataUpdatedEvent != null) {
                List<IncomingAudioAPIData> changedPeers = new List<IncomingAudioAPIData>();
                lock (_peerDataMap) {
                    foreach (string key in _changedPeerKeys) {
                        IncomingAudioAPIData peer;
                        if (_peerDataMap.TryGetValue(key, out peer)) {
                            changedPeers.Add(peer.DeepCopy());
                        }
                    }
                    _changedPeerKeys.Clear();
                }
                if (changedPeers.Count > 0) {
                    PeerDataUpdatedEvent?.Invoke(changedPeers);
                }
            }
        }
        if (_peersHaveDisconnected) {
            // grab reference to _deletedVisitIds
            SortedSet<string> deletedVisitIds = _deletedVisitIds;
            lock (_peerDataMap) {
                // swap in a new _deletedVisitIds while under lock
                _peersHaveDisconnected = false;
                _deletedVisitIds = new SortedSet<string>();
            }
            PeerDisconnectedEvent?.Invoke(deletedVisitIds);
        }
    }

    private void DumpPeerData() { // debug
        lock (_peerDataMap) {
            foreach (KeyValuePair<string, IncomingAudioAPIData> kvp in _peerDataMap) {
                LogUtil.LogDebug(this, "DumpPeerData key={0} value={1}",
                    kvp.Key, kvp.Value.ToWireFormattedJsonString());
            }
        }
    }

    private void OnDestroy() {
    }

    public void Connect() {
        LogUtil.LogUncommonEvent(this, "Connect");
        SanityCheckSignalingServiceUrl();
        // The HiFi SignalingServiceUrl expects the JWT token on the end
        string signalUrl = SignalingServiceUrl + "?token=" + JWT;

        RaviSession.Open(signalUrl);

        // add command handlers
        RaviSession.CommandController.AddHandler("audionet.init", HandleAudionetInit);
        RaviSession.CommandController.BinaryCommandHandler = HandleAudionetBinaryData;
    }

    public void Disconnect() {
        // TODO: implement this
        RaviSession.Close();
    }

    public void SetInputAudioMediaStream() {
        // TODO?: implement this?
    }

    private void CreateRaviSession() {
        LogUtil.LogUncommonEvent(this, "CreateRaviSession");
        RaviSession = gameObject.AddComponent<Ravi.RaviSession>() as Ravi.RaviSession;
    }

    private void SanityCheckSignalingServiceUrl() {
        // sanity check SignalingServiceUrl
        string originalUrl = SignalingServiceUrl;
        if (string.IsNullOrEmpty(SignalingServiceUrl)) {
            SignalingServiceUrl = HiFi.Constants.HIFI_API_SIGNALING_URL;
        } else {
            if (!SignalingServiceUrl.StartsWith("ws://") && !SignalingServiceUrl.StartsWith("wss://")) {
                SignalingServiceUrl = "ws://" + SignalingServiceUrl;
            }
            if (!SignalingServiceUrl.EndsWith("/")) {
                SignalingServiceUrl += "/";
            }
        }
        if (originalUrl != SignalingServiceUrl) {
            LogUtil.LogUncommonEvent(this, "SanityCheckSignalingServiceUrl '{0}'-->'{1}'", originalUrl, SignalingServiceUrl);
        }
    }

    private void UpdateState(AudionetConnectionState newState) {
        if (_connectionState != newState) {
            LogUtil.LogUncommonEvent(this, "UpdateState: '{0}'-->'{1}'", _connectionState, newState);
            _connectionState = newState;
            // fire the event later when we're on main thread
            _stateHasChanged = true;
        }
    }

    private bool SendAudionetInit() {
        LogUtil.LogDebug(this, "SendAudionetInit");
        if (RaviSession != null && RaviSession.CommandController != null) {
            if (_connectionState == AudionetConnectionState.Failed) {
                UpdateState(AudionetConnectionState.Disconnected);
            }
            JSONNode payload = new JSONObject();
            payload["primary"] = true;
            payload["visit_id"] = RaviSession.SessionId;
            payload["session"] = RaviSession.SessionId;
            payload["streaming_scope"] = UserDataScopeStrings[(int) UserDataStreamingScope];
            payload["is_input_stream_stereo"] = InputAudioIsStereo;
            if (_connectionState == AudionetConnectionState.Disconnected) {
                UpdateState(AudionetConnectionState.Connecting);
            }
            LogUtil.LogUncommonEvent(this, "SEND audionet.init");
            bool success = RaviSession.CommandController.SendCommand("audionet.init", payload);
            if (!success) {
                LogUtil.LogWarning(this, "SEND audionet.init failed");
                UpdateState(AudionetConnectionState.Failed);
            }
            return success;
        }
        LogUtil.LogError(this, "SendAudionetInit failed for null RaviSession or CommandController");
        return false;
    }

    private bool SendUserData() {
        AudioAPIDataChanges changes = _lastUserData.ApplyAndGetChanges(_userData);
        if (!changes.IsEmpty()) {
            return RaviSession.CommandController.SendInput(changes.ToWireFormattedJsonString());
        }
        // although we didn't send anything, consider this success
        return true;
    }

    private void HandleAudionetInit(string msg) {
        try {
            if (_connectionState == AudionetConnectionState.Connecting) {
                JSONNode obj = JSONNode.Parse(msg);
                _mixerInfo.buildNumber = obj["build_number"];
                _mixerInfo.buildType = obj["build_type"];
                _mixerInfo.buildVersion = obj["build_version"];
                _mixerInfo.visitIdHash = obj["visit_id_hash"];
                _mixerInfo.visitId = RaviSession.SessionId;
                UpdateState(AudionetConnectionState.Connected);
            } else {
                LogUtil.LogWarning(this, "HandleAudionetInit RECV audionet.init response with unexpected connectionState='{0}'", _connectionState);
            }
        } catch (Exception e) {
            LogUtil.LogError(this, "HandleAudionetInit failed to parse message='{0}' err='{1}'", msg, e.Message);
        }
    }

    private void HandleAudionetBinaryData(byte[] data) {
        LogUtil.LogDebug(this, "HandleAudionetBinaryData data.Length={0}", data.Length);
        // 'data' may be gzipped so we first try to decompress it
        const int MAX_UNCOMPRESSED_BUFFER_SIZE = 1024;
        byte[] uncompressedData = new byte[MAX_UNCOMPRESSED_BUFFER_SIZE];
        try {
            using(MemoryStream zipped = new MemoryStream(data)) {
                using(GZipStream unzipper = new GZipStream(zipped, CompressionMode.Decompress)) {
                    int numBytes = unzipper.Read(uncompressedData, 0, uncompressedData.Length);
                    string uncompressedText = System.Text.Encoding.UTF8.GetString(uncompressedData, 0,  numBytes);
                    LogUtil.LogDebug(this, "HandleAudionetBinaryData uncompressedText='{0}'", uncompressedText);
                }
            }
        } catch (Exception) {
            // decompression failed
            uncompressedData = data;
        }

        string text = System.Text.Encoding.UTF8.GetString(uncompressedData);
        lock (_peerDataMap) {
            try {
                //RaviSessionBinaryData sessionData = JsonUtility.FromJson<RaviSessionBinaryData>(text);
                JSONNode obj = JSONNode.Parse(text);
                if (obj.HasKey("peers")) {
                    JSONNode peers = obj["peers"];
                    bool somethingChanged = false;
                    foreach (string key in peers.Keys) {
                        JSONNode peerInfo = peers[key];
                        try {
                            somethingChanged = _peerDataMap[key].ApplyWireFormattedJson(peerInfo) || somethingChanged;
                        } catch (KeyNotFoundException) {
                            IncomingAudioAPIData d = new IncomingAudioAPIData();
                            somethingChanged = d.ApplyWireFormattedJson(peerInfo);
                            _peerDataMap.Add(key, d);
                            if (peerInfo.HasKey("e")) {
                                // unforunately the "key" to IncomingAudioAPIData is not the same "key"
                                // used to indicate a peer has disconnected, so we maintain a Map
                                // between these two keys to quickly figure out which peers are being
                                // disconnected
                                string hashedVisitId = peerInfo["e"];
                                if (_peerKeyMap.ContainsKey(hashedVisitId)) {
                                    _peerKeyMap[hashedVisitId] = key;
                                } else {
                                    _peerKeyMap.Add(hashedVisitId, key);
                                }
                            }
                            _changedPeerKeys.Add(key);
                        }
                    }
                    if (somethingChanged) {
                        _peerDataHasChanged = true;
                    }
                }
                if (obj.HasKey("deleted_visit_ids")) {
                    JSONNode ids = obj["deleted_visit_ids"];
                    if (ids.IsArray) {
                        foreach (JSONNode id in ids) {
                            string hashedVisitId = id.Value;
                            string key;
                            if (_peerKeyMap.TryGetValue(hashedVisitId, out key)) {
                                _peerDataMap.Remove(key);
                                _deletedVisitIds.Add(hashedVisitId);
                            }
                            _peerKeyMap.Remove(hashedVisitId);
                        }
                    }
                    _peersHaveDisconnected = true;
                }
            } catch (Exception e) {
                LogUtil.LogError(this, "HandleAudionetBinaryData failed to parse RaviSessionBinaryData err='{0}'", e.Message);
            }
        }
    }

    public void OnRaviSessionStateChanged(Ravi.RaviSession.SessionState state) {
        if (state == Ravi.RaviSession.SessionState.Connected) {
            bool success = SendAudionetInit();
            if (!success) {
                // TODO?: is there a way to recover from this?  Do we care?
            }
        }
    }
}

} // namespace Ravi


