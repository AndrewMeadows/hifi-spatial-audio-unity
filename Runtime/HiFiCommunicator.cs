/*
 * The [[HiFiCommunicator]] component provides the main API for using the High Fidelity Audio Service
 * - `ConnectToHiFiAudioAPIServer()`: Connect to High Fidelity Audio Server
 * - `DisconnectFromHiFiAudioAPIServer()`: Disconnect from High Fidelity Audio Server
 * - Setters/Getters for the User's various HiFiAudioAPIData variable members.
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
using Ravi;

namespace HiFi {

public struct HiFiMixerInfo {
    public string buildNumber;
    public string buildType;
    public string buildVersion;
    public string visitId;
    public string visitIdHash;
}

/// <summary>
/// Helper class for tracking local changes to be transmitted to Server.
/// </summary>
/// <see cref="UserData"/>
public class UserDataWrapper {
    public OutgoingAudioAPIData data { get; internal set; }
    public bool hasChanged { get; set; }

    public UserDataWrapper() {
        data = new OutgoingAudioAPIData();
        hasChanged = false;
    }

    public Vector3 Position {
        set {
            data.position = value;
            hasChanged = true;
        }
        get { return data.position; }
    }

    public Quaternion Orientation {
        set {
            data.orientation = value;
            hasChanged = true;
        }
        get { return data.orientation; }
    }

    public float VolumeThreshold {
        set {
            data.volumeThreshold = value;
            hasChanged = true;
        }
        get { return data.volumeThreshold; }
    }

    public float Gain {
        set {
            data.hiFiGain = value;
            hasChanged = true;
        }
        get { return data.hiFiGain; }
    }

    public float Attenuation {
        set {
            data.userAttenuation = value;
            hasChanged = true;
        }
        get { return data.userAttenuation; }
    }

    public float Rolloff {
        set {
            data.userRolloff = value;
            hasChanged = true;
        }
        get { return data.userRolloff; }
    }
}

/// <summary>
/// Component for access to HiFi Spatial Audio API.
/// </summary>
/// <remarks>
/// The overview of using the HiFi Spatial Audio Service in Unity is as follows:
/// <list type="number">
///   <listheader>
///      <description>
///      Given an ip:port of the HiFi Spatial Audio Service and a <c>JWT</c> (Java Web Token)...
///      </description>
///   </listheader>
///   <item>
///      <description>
///      On <c>Start()</c>: Create a <c>HiFiCommunicator</c> instance (we'll call it <c>hiFiCommunicator</c>),
///      set the two above Propeties,
///      and call <c>hiFiCommunicator.ConnectToHiFiAudioAPIServer();</c>
///      </description>
///   </item>
///   <item>
///      <description>
///      Update <c>hiFiCommunicator.Position</c> and <c>.Orientation</c> as necessary.
///      </description>
///   </item>
///   <item>
///      <description>
///      The <c>hiFiCommunicator.Update()</c> method will automatically send updates to the HiFi Spatial Audio Service.
///      </description>
///   </item>
///   <item>
///      <description>
///      If you want to receive HiFi Spatial Audio data about other Users in the Space then on <c>Start()</c> also:
///      set <c>hiFiCommunicator.UserDataStreamingScope = HiFiCommunicator.UserDataScope.Peers;</c>,
///      and register an event handler to <c>hiFiCommunicator.PeerDataUpdatedEvent</c> to harvest peer changes as they arrive,
///      and also an event handler to <c>hiFiCommunicator.PeerDisconnectedEvent</c> to handle when User's leave the Space.
///      </description>
///   </item>
///</list>
/// </remarks>
[AddComponentMenu("HiFi Spatial Audio Communicator")]
[Serializable]
public class HiFiCommunicator : MonoBehaviour {
    /// <summary name="AudionetConnectionState">
    /// Possible connection states values between HiFiCommunicator and HiFi Spatial Audio Service
    /// </summary>
    /// <see cref="ConnectionState"/>
    public enum AudionetConnectionState {
        Signaling = 0,
        Connecting,
        Connected,
        Disconnected,
        Failed,
        Unavailable // e.g. when API Server is at capacity
    }

    /// <summary name="UserDataScope">
    /// Possible scopes allowed for User data subscription from HiFi Spatial Audio Service
    /// </summary>
    /// <list type="definition">
    ///   <listheader>
    ///      <term>UseDataScope</term>
    ///      <description>enum</description>
    ///   </listheader>
    ///   <item>
    ///      <term>None</term>
    ///      <description>no User data whatsoever</description>
    ///   </item>
    ///   <item>
    ///      <term>Peers</term>
    ///      <description>User data about peers but not this User</description>
    ///   </item>
    ///   <item>
    ///      <term>All</term>
    ///      <description>User data about peers including this User</description>
    ///   </item>
    ///</list>
    /// <see cref="UserDataStreamingScope"/>
    public enum UserDataScope {
        None = 0,
        Peers,
        All
    }

    static string[] UserDataScopeStrings = { "none", "peers", "all" };

    /// <summary name="ConnectionState">
    /// State of connection to HiFi Spatial Audio Service
    /// </summary>
    /// <see cref="AudionetConnectionState"/>
    /// <seealso cref="ConnectionStateChangedEvent"/>
    public AudionetConnectionState ConnectionState {
        get; internal set;
    }

    /// <summary name="UserDataStreamingScope">
    /// Scope of available user data to be streamed from HiFi Spatial Audio Service
    /// </summary>
    /// <remarks>Default value is 'None'</remarks>
    /// <see cref="UserDataScope"/>
    public UserDataScope UserDataStreamingScope = UserDataScope.None;


    /// <summary name="SignalingServiceUrl">
    /// IP:port to websocket of HiFi Spatial Audio Service
    /// </summary>
    [Tooltip("SignalUrl (with :port but without JWT)")]
    public string SignalingServiceUrl = HiFi.HiFiConstants.HIFI_API_SIGNALING_URL;

    /// <summary>Json Web Token</summary>
    /// <remarks>
    /// The JWT stores the providedUserID (aka "publicly visible user name")
    /// of this connection as well as the "Space" to connect to and possibly other
    /// Space/session relevant meta data such as expiry.  Typically these are
    /// generated or commissioned by the client's Application on a
    /// per-user basis.
    /// </remarks>
    [Tooltip("Json Web Token (client/server identification, and session info)")]
    public string JWT;

    /// <summary name="RaviSession">RaviSession component</summary>
    /// <remarks>
    /// HiFi Spatial Audio is built on top of a lower protocol called "Ravi".
    /// </remarks>
    [Tooltip("RaviSession")]
    public RaviSession RaviSession;

    float _userDataUpdatePeriod = 50.0f; // msec, 20 Hz

    /// <summary name="UserDataUpdatePeriod">
    /// Minimum time (msec) between between OutgoingAudioAPIData updates to HiFi service.
    /// </summary>
    /// <remarks>
    /// Default value is 50 msec (20 Hz).
    /// </remarks>
    public float UserDataUpdatePeriod {
        set {
            const float MIN_USER_DATA_UPDATE_PERIOD = 20.0f; // msec
            const float MAX_USER_DATA_UPDATE_PERIOD = 5000.0f; // msec
            _userDataUpdatePeriod = Mathf.Clamp(value, MIN_USER_DATA_UPDATE_PERIOD, MAX_USER_DATA_UPDATE_PERIOD);
        }
        get { return _userDataUpdatePeriod; }
    }

    DateTime _userDataUpdateExpiry;

    /// <summary name="ConnectionStateChangedDelegate">
    /// The function signature for the ConnectionStateChangedEvent
    /// </summary>
    /// <param name="state">The new AudionetConnectionState of the HiFiCommunicator.</param>
    /// <see cref="ConnectionStateChangedEvent"/>
    /// <seealso cref="AudionetConnectionState"/>
    public delegate void ConnectionStateChangedDelegate(AudionetConnectionState state);

    /// <summary name="OnPeerDataUpdatedDelegate">
    /// The function signature for the PeerDataUpdatedEvent
    /// </summary>
    /// <param name="peers">
    /// A List of IncomingAudioAPIData for all peers changed in any way since the last frame.
    /// </param>
    /// <see cref="PeerDataUpdatedEvent"/>
    public delegate void OnPeerDataUpdatedDelegate(List<IncomingAudioAPIData> peers);

    /// <summary name="OnPeerDisconnectedDelegate">
    /// The function signature for the PeerDisconnectedEvent
    /// </summary>
    /// <param name="ids">
    /// A List of hashedVisitIds for all peers removed from the audio space since the last frame.
    /// </param>
    /// <see cref="PeerDisconnectedEvent"/>
    public delegate void OnPeerDisconnectedDelegate(SortedSet<string> ids);

    /// <summary name="ConnectionStateChangedEvent">
    /// This event fires on the main thread after the ConnectionState changes.
    /// </summary>
    public event ConnectionStateChangedDelegate ConnectionStateChangedEvent;

    /// <summary name="PeerDataUpdatedEvent">
    /// This event fires on the main thread after IncomingAudioAPIData has arrived from the HiFi Spatial Audio Service.
    /// </summary>
    /// <see cref="OnPeerDataUpdatedDelegate"/>
    public event OnPeerDataUpdatedDelegate PeerDataUpdatedEvent;

    /// <summary name="PeerDisconnectedEvent">
    /// This event fires on the main thread after one or more peer in the same space has been disconnected from the HiFi Spatial Audio Service.
    /// </summary>
    /// <see cref="OnPeerDisconnectedDelegate"/>
    public event OnPeerDisconnectedDelegate PeerDisconnectedEvent;

    /// <summary name="UserData">
    /// Property for communicating local user data changes to be sent to HiFi Spatial Audio Service.
    /// </summary>
    /// <see cref="UserDataWrapper"/>
    public UserDataWrapper UserData { get; internal set; }

    HiFiMixerInfo _mixerInfo;
    Dictionary<string, IncomingAudioAPIData> _peerDataMap;
    Dictionary<string, string> _peerKeyMap; // hashdVisitId-->"peer key"
    SortedSet<string> _changedPeerKeys;
    SortedSet<string> _deletedVisitIds;

    bool _muteMic = false;
    OutgoingAudioAPIData _lastUserData;

    byte[] _uncompressedData;
    bool _stateHasChanged = false;
    bool _peerDataHasChanged = false;
    bool _peersHaveDisconnected = false;

    void Awake() {
        const int MAX_UNCOMPRESSED_BUFFER_SIZE = 65536;
        _uncompressedData = new byte[MAX_UNCOMPRESSED_BUFFER_SIZE];

        ConnectionState = AudionetConnectionState.Disconnected;
        if (RaviSession == null) {
            CreateRaviSession();
        }
        RaviSession.SessionStateChangedEvent += OnRaviSessionStateChanged;

        UserData = new UserDataWrapper();
        _lastUserData = new OutgoingAudioAPIData();
        // HACK: we twiddle _lastUserData members to odd values to ensure the first
        // update is actually sent (e.g. because we only send changes ==> force first update).
        _lastUserData.position = new Vector3(-1.0e7f, -1.0e7f, -1.0e7f);
        _lastUserData.orientation = new Quaternion(-1.0e7f, -1.0e7f, -1.0e7f, -1.0e7f);
        _lastUserData.volumeThreshold = -96.0f;
        _lastUserData.hiFiGain = -1.0e7f;
        //_lastUserData.userAttenuation = -1.0e7f; // don't twiddle this one
        //_lastUserData.userRolloff = -1.0e7f; // don't twiddle this one

        _peerDataMap = new Dictionary<string, IncomingAudioAPIData>();
        _peerKeyMap = new Dictionary<string, string>();
        _changedPeerKeys = new SortedSet<string>();
        _deletedVisitIds = new SortedSet<string>();

        // By default disable most logging.
        // This can be overrdden for debugging by external code AFTER this hard-coded setting.
        // Pick one of the lines below:
        //Log.GlobalMaxLevel = Log.Level.Silent;
        Log.GlobalMaxLevel = Log.Level.UncommonEvent;
        //Log.GlobalMaxLevel = Log.Level.CommonEvent;
        //Log.GlobalMaxLevel = Log.Level.Debug;
    }

    void Start() {
        _userDataUpdateExpiry = DateTime.Now;
    }

    void Update() {
        if (_stateHasChanged) {
            _stateHasChanged = false;
            ConnectionStateChangedEvent?.Invoke(ConnectionState);
        }
        if (_peerDataHasChanged) {
            // it is OK to reset _peerDataHasChanged outside of the lock
            // because the consequences of a race condition here would produce an empty _changedPeerKeys
            // on the next frame, and its handling is fault-tolerant for the empty set.
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

        if (ConnectionState != AudionetConnectionState.Connected) {
            return;
        }

        if (!UserData.hasChanged) {
            return;
        }

        DateTime now = DateTime.Now;
        if (now < _userDataUpdateExpiry) {
            // do nothing yet
            return;
        }
        _userDataUpdateExpiry.AddMilliseconds(_userDataUpdatePeriod);
        if (_userDataUpdateExpiry < now) {
            _userDataUpdateExpiry = now.AddMilliseconds(_userDataUpdatePeriod);
        }

        UserData.hasChanged = false;
        TransmitHiFiAudioAPIDataToServer();
    }

    // for debug purposes
    void DumpPeerData() {
        lock (_peerDataMap) {
            foreach (KeyValuePair<string, IncomingAudioAPIData> kvp in _peerDataMap) {
                Log.Debug(this, "DumpPeerData key={0} value={1}",
                    kvp.Key, kvp.Value.ToWireFormattedJsonString());
            }
        }
    }

    void OnDestroy() {
    }

    /// <summary>
    /// Connect to HiFi Spatial Audio Service.
    /// </summary>
    /// <remarks>
    /// SignalingServiceUrl and JWT must be set prior to connect.
    /// </remarks>
    public void ConnectToHiFiAudioAPIServer() {
        Log.UncommonEvent(this, "ConnectToHiFiAudioAPIServer");
        if (ConnectionState == AudionetConnectionState.Failed) {
            UpdateState(AudionetConnectionState.Disconnected);
        }
        if (ConnectionState == AudionetConnectionState.Disconnected) {
            UpdateState(AudionetConnectionState.Signaling);
            SanityCheckSignalingServiceUrl();

            // RaviSession.Connect expects the the full URL with JWT token on the end
            string signalUrl = SignalingServiceUrl + "?token=" + JWT;
            RaviSession.Connect(signalUrl);

            // add command handlers
            RaviSession.CommandController.AddCommandHandler("audionet.init", HandleAudionetInit);
            RaviSession.CommandController.AddCommandHandler("audionet.personal_volume_adjust", HandlePersonalVolumeAdjust);
            RaviSession.CommandController.BinaryCommandHandler = HandleAudionetBinaryData;
        }
    }

    /// <summary>
    /// Disconnect from HiFi Spatial Audio Service.
    /// </summary>
    public void DisconnectFromHiFiAPIServer() {
        RaviSession.Close();
        RemoveRaviSessionHandlers();
    }

    /// <summary>
    /// Mute/unmute local audio input to HiFi Spatial Audio Service.
    /// </summary>
    /// <param name="muted">True if audio should be muted, else false.</param>
    public void SetInputAudioMuted(bool muted) {
        if (_muteMic != muted) {
            _muteMic = muted;
            // BUG: Unity's webrtc plugin does not yet expose the ability to mute the mic
            // WORKAROUND: on mute/unmute we adjust the hiFiGain and volumeThreshild submitted to Server
            // to achieve server-side mute/unmute, which is why we need to flag UserData as changed
            UserData.hasChanged = true;
        }
    }

    /// <summary>
    /// Adjust the volume of a peer's audio for this user.
    /// </summary>
    /// <param name="visitIdHash">Unique string for target user.</param>
    /// <param name="gain">Float value in range [0,1].</param>
    /// <returns>True if request was sent to HiFi Spatial Audio Service.</returns>
    public bool SetOtherUserGainForThisConnection(string visitIdHash, float gain) {
        Log.Debug(this, "SendOtherUserGainForThisConnection id='{}' gain={}", visitIdHash, gain);
        if (ConnectionState == AudionetConnectionState.Connected) {
            JSONNode payload = new JSONObject();
            payload["visit_id_hash"] = visitIdHash;
            payload["gain"] = gain;
            bool success = RaviSession.CommandController.SendCommand("audionet.personal_volume_adjust", payload);
            if (!success) {
                Log.Warning(this, "SEND audionet.personal_volume_adjust failed");
            }
            return success;
        }
        return false;
    }

    void RemoveRaviSessionHandlers() {
        RaviSession.CommandController.RemoveCommandHandler("audionet.init");
        RaviSession.CommandController.RemoveCommandHandler("audionet.personal_volume_adjust");
        RaviSession.CommandController.BinaryCommandHandler = null;
    }

    void CreateRaviSession() {
        Log.UncommonEvent(this, "CreateRaviSession");
        RaviSession = gameObject.AddComponent<RaviSession>() as RaviSession;
    }

    void SanityCheckSignalingServiceUrl() {
        // sanity check SignalingServiceUrl
        string originalUrl = SignalingServiceUrl;
        if (string.IsNullOrEmpty(SignalingServiceUrl)) {
            SignalingServiceUrl = HiFi.HiFiConstants.HIFI_API_SIGNALING_URL;
        } else {
            if (!SignalingServiceUrl.StartsWith("ws://") && !SignalingServiceUrl.StartsWith("wss://")) {
                SignalingServiceUrl = "ws://" + SignalingServiceUrl;
            }
            if (!SignalingServiceUrl.EndsWith("/")) {
                SignalingServiceUrl += "/";
            }
        }
        if (originalUrl != SignalingServiceUrl) {
            Log.UncommonEvent(this, "SanityCheckSignalingServiceUrl '{0}'-->'{1}'", originalUrl, SignalingServiceUrl);
        }
    }

    void UpdateState(AudionetConnectionState newState) {
        if (ConnectionState != newState) {
            if (newState == AudionetConnectionState.Failed) {
                RemoveRaviSessionHandlers();
            }
            Log.UncommonEvent(this, "UpdateState: '{0}'-->'{1}'", ConnectionState, newState);
            ConnectionState = newState;
            // fire the event later when we're definitely on main thread
            _stateHasChanged = true;
        }
    }

    bool SendAudionetInit() {
        Log.Debug(this, "SendAudionetInit");
        if (RaviSession != null && RaviSession.CommandController != null) {
            if (ConnectionState == AudionetConnectionState.Failed) {
                UpdateState(AudionetConnectionState.Disconnected);
            }
            JSONNode payload = new JSONObject();
            payload["primary"] = true;
            payload["visit_id"] = RaviSession.SessionId;
            payload["session"] = RaviSession.SessionId;
            payload["streaming_scope"] = UserDataScopeStrings[(int) UserDataStreamingScope];
            // stereo upload to HiFi Spatial Audio Service is an experimental feature
            // and is not supported in Unity yet.
            bool INPUT_AUDIO_IS_STEREO = false;
            payload["is_input_stream_stereo"] = INPUT_AUDIO_IS_STEREO;

            UpdateState(AudionetConnectionState.Connecting);
            Log.UncommonEvent(this, "SEND audionet.init");
            bool success = RaviSession.CommandController.SendCommand("audionet.init", payload);
            if (!success) {
                Log.Warning(this, "SEND audionet.init failed");
                UpdateState(AudionetConnectionState.Failed);
            }
            return success;
        }
        Log.Error(this, "SendAudionetInit failed for null RaviSession or CommandController");
        return false;
    }

    bool TransmitHiFiAudioAPIDataToServer() {
        // BUG: Unity's webrtc plugin does not yet expose the ability to mute the mic
        // WORKAROUND: when muted we slam the hiFiGain and volumeThreshold submitted to the Server
        // to achieve server-side mute
        OutgoingAudioAPIData data = UserData.data.DeepCopy();
        if (_muteMic) {
            data.hiFiGain = 0.0f;
            data.volumeThreshold = 0.0f;
        }
        AudioAPIDataChanges changes = _lastUserData.ApplyAndGetChanges(data);
        if (!changes.IsEmpty()) {
            return RaviSession.CommandController.SendInput(changes.ToWireFormattedJsonString());
        }
        // although we didn't send anything, consider this success
        return true;
    }

    void HandleAudionetInit(string msg) {
        Log.UncommonEvent(this, "HandleAudionetInit RECV audionet.init response msg='{0}'", msg);
        try {
            if (ConnectionState == AudionetConnectionState.Connecting) {
                JSONNode obj = JSONNode.Parse(msg);
                _mixerInfo.buildNumber = obj["build_number"];
                _mixerInfo.buildType = obj["build_type"];
                _mixerInfo.buildVersion = obj["build_version"];
                // Note: _mixerInfo.visitIdHash can be used to figure out which of the "peers" data
                // corresponds to our current HiFiSession when using UserDataScope=All
                _mixerInfo.visitIdHash = obj["visit_id_hash"];
                _mixerInfo.visitId = RaviSession.SessionId;

                UpdateState(AudionetConnectionState.Connected);
            } else {
                Log.Warning(this, "HandleAudionetInit RECV audionet.init response with unexpected connectionState='{0}'", ConnectionState);
            }
        } catch (Exception e) {
            Log.Error(this, "HandleAudionetInit failed to parse message='{0}' err='{1}'", msg, e.Message);
        }
    }

    void HandlePersonalVolumeAdjust(string msg) {
        Log.CommonEvent(this, "HandleAudionetInit RECV audionet.personal_volume_adjust response msg='{0}'", msg);
        try {
            JSONNode obj = JSONNode.Parse(msg);
            bool success = obj["success"];
            if (!success) {
                Log.UncommonEvent(this, "HandlePersonalVolumeAdjust failed for reason='{0}'", obj["reason"]);
            }
        } catch (Exception e) {
            Log.Error(this, "HandlePersonalVolumeAdjust failed to parse message='{0}' err='{1}'", msg, e.Message);
        }
    }

    void HandleAudionetBinaryData(byte[] data) {
        Log.Debug(this, "HandleAudionetBinaryData data.Length={0}", data.Length);
        // 'data' may be gzipped so we first try to decompress it
        // but first we lock _uncompressedData: a big buffer we don't
        // allocate on the fly for theoretical performance issues
        string text;
        lock (_uncompressedData) {
            try {
                using(MemoryStream zipped = new MemoryStream(data)) {
                    using(GZipStream unzipper = new GZipStream(zipped, CompressionMode.Decompress)) {
                        int numBytes = unzipper.Read(_uncompressedData, 0, _uncompressedData.Length);
                        text = System.Text.Encoding.UTF8.GetString(_uncompressedData, 0,  numBytes);
                        Log.Debug(this, "HandleAudionetBinaryData uncompressedText='{0}'", text);
                    }
                }
            } catch (Exception) {
                // decompression failed --> try to parse non-compressed data
                text = System.Text.Encoding.UTF8.GetString(data);
            }
        }
        lock (_peerDataMap) {
            try {
                JSONNode obj = JSONNode.Parse(text);
                if (obj.HasKey("peers")) {
                    JSONNode peers = obj["peers"];
                    foreach (string key in peers.Keys) {
                        bool somethingChanged = false;
                        JSONNode peerInfo = peers[key];
                        try {
                            somethingChanged = _peerDataMap[key].ApplyWireFormattedJson(peerInfo);
                        } catch (KeyNotFoundException) {
                            IncomingAudioAPIData d = new IncomingAudioAPIData();
                            d.ApplyWireFormattedJson(peerInfo);
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
                            somethingChanged = true;
                        }
                        if (somethingChanged) {
                            _changedPeerKeys.Add(key);
                            _peerDataHasChanged = true;
                        }
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
                Log.Error(this, "HandleAudionetBinaryData failed to parse RaviSessionBinaryData err='{0}'", e.Message);
            }
        }
    }

    void OnRaviSessionStateChanged(RaviSession.SessionState state) {
        if (state == RaviSession.SessionState.Connected) {
            bool success = SendAudionetInit();
            if (!success) {
                // TODO?: is there a way to recover from this?  Do we care?
                UpdateState(AudionetConnectionState.Failed);
            }
        }
    }
}

} // namespace Ravi


