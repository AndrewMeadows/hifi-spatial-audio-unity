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
    public string userId;
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

    // MuteState describes the current and intended state of the client's input
    // audio mute (e.g. from the microphone).  It is passed as a parameter
    // to Observer::onMuteStateChange() callback.
    public class MuteState {
        // Whether the mute state agrees with last attempted changed.  This can
        // be 'false' if the client is trying to unmute when adminPreventsUnmuting
        // is true (e.g. after a server-side "mute=true instruciton") or if there
        // was a failure setting the mute state of the input device.
        public bool success = false;

        // Indicates the muted value that would have been set if the mute state
        // was set succesfully.  `true` means muted, `false` means unmuted.
        // The client changes this value via Communicator::setInputAudioMuted().
        // The server changes this value via a "mute instruction".
        public bool targetMuteValue = false;

        // Indicates the current muted value after attempting to set mute state.
        // `true` means muted, `false` means unmuted.
        public bool currentMuteValue = false;

        // Indicates whether the client is currently prevented from using
        // Communicator::setInputAudioMuted() to unmuting because of server-side
        // "mute instruction".
        public bool adminPreventsUnmuting = false;

        // Indicates the reason the mute state has changed.
        public string reason = "unknown";

        public MuteState DeepCopy() {
            MuteState m = new MuteState();
            m.success = success;
            m.targetMuteValue = targetMuteValue;
            m.currentMuteValue = currentMuteValue;
            m.adminPreventsUnmuting = adminPreventsUnmuting;
            m.reason = reason;
            return m;
        }
    }

    const long THE_DISTANT_FUTURE = Int64.MaxValue;

    /// <summary name="AudionetConnectionState">
    /// Possible connection states values between HiFiCommunicator and HiFi Spatial Audio Service
    /// </summary>
    /// <see cref="ConnectionState"/>
    public enum AudionetConnectionState {
        Disconnected = 0,
        Connecting,
        Connected,
        Reconnecting,
        Disconnecting,
        Failed,
        Unavailable // e.g. when API Server is at capacity
    }

    const long TICKS_PER_MSEC = 10000; // 1 tick = 100 nsec
    const long TICKS_PER_SECOND = 10000000;

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
    /// <remarks>Default value is 'All'</remarks>
    /// <see cref="UserDataScope"/>
    public UserDataScope UserDataStreamingScope = UserDataScope.All;


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
    /// generated or commissioned by the client's Application on a per-user basis.
    /// </remarks>
    [Tooltip("Json Web Token (client/server identification, and session info)")]
    public string JWT;

    /// <summary>
    /// Whether or not input audio should be muted.
    /// </summary>
    public bool InputAudioMuted {
        set {
            bool mute_state_changed = false;
            MuteState mute_state = new MuteState();
            lock(_muteState) {
                bool was_muted = _muteState.currentMuteValue;
                if (_raviSession != null) {
                    if (value) {
                        _raviSession.InputAudioMuted = value;
                        _muteState.reason = "local mute";
                    } else {
                        if (_muteState.adminPreventsUnmuting) {
                            _muteState.reason = "local unmute prevented by remote admin";
                            mute_state_changed = true;
                        } else {
                            _raviSession.InputAudioMuted = value;
                            _muteState.reason = "local unmute";
                        }
                    }
                    _muteState.currentMuteValue = _raviSession.InputAudioMuted;
                } else {
                    // there is no session so we are effectively muted
                    _muteState.currentMuteValue = true;
                    _muteState.reason = "no session";
                }
                if (_muteState.targetMuteValue != value) {
                    mute_state_changed = true;
                }
                _muteState.targetMuteValue = value;
                _muteState.success = (_muteState.currentMuteValue == value);
                if (was_muted != _muteState.currentMuteValue || mute_state_changed) {
                    mute_state_changed = true;
                    mute_state = _muteState.DeepCopy();
                }
            }
            if (mute_state_changed) {
                MuteStateChangedEvent?.Invoke(mute_state);
            }
        }
        get {
            // Note: we could ask the _raviSession like so:
            //if (_raviSession != null) {
            //    return _raviSession.InputAudioMuted;
            //}
            // but instead we assume _muteState is correct and up to date
            bool is_muted = false;
            lock(_muteState) {
                is_muted = _muteState.currentMuteValue;
            }
            return is_muted;
        }
    }

    RaviSession _raviSession;

    long _userDataUpdatePeriod = 50; // msec, 20 Hz

    /// <summary name="ConnectionConfig">Config for reconnection and timeouts</summary>
    /// <remarks>
    /// AutoRetryConnection = whether to retry initial connection, false by default
    /// RetryConnectionTimeout = how long to retry initial connection, 15 seconds by default
    /// AutoReconnect = whether to reconnect on broken connection, false by default
    /// ReconnectionTimeout = how long to attempt reconnection, 60 seconds by default
    /// ConnectionDelayMs = delay after failed connection before starting next, 500 msec by default
    /// ConnectionTimeoutMs = how long to wait for each connection attempt, 5000 msec by default
    /// </remarks>
    public HiFiConnectionAndTimeoutConfig ConnectionConfig;

    /// <summary name="UserDataUpdatePeriod">
    /// Minimum time (msec) between between OutgoingAudioAPIData updates to HiFi service.
    /// </summary>
    /// <remarks>
    /// Default value is 50 msec (20 Hz).
    /// </remarks>
    public long UserDataUpdatePeriod {
        set {
            const long MIN_USER_DATA_UPDATE_PERIOD = 20; // msec
            const long MAX_USER_DATA_UPDATE_PERIOD = 5000; // msec
            if (value < MIN_USER_DATA_UPDATE_PERIOD) {
                value = MIN_USER_DATA_UPDATE_PERIOD;
            } else if (value > MAX_USER_DATA_UPDATE_PERIOD) {
                value = MAX_USER_DATA_UPDATE_PERIOD;
            }
            _userDataUpdatePeriod = value;
        }
        get { return _userDataUpdatePeriod; }
    }

    long _userDataUpdateExpiry; // ticks (100 nsec)
    long _connectingExpiry;
    long _reconnectingExpiry;
    long _attemptExpiry;
    long _nextAttempt;

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

    /// <summary name="OnMuteStateChangedDelegate">
    /// The function signature for the MuteStateChangedEvent
    /// </summary>
    /// <param name="mute_state">
    /// A copy of the Communicator's MuteState
    /// </param>
    /// <see cref="MuteStateChangedEvent"/>
    public delegate void OnMuteStateChangedDelegate(MuteState mute_state);

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

    /// <summary name="MuteStateChangedEvent">
    /// This event fires on the main thread after the mute state of the webrtc connection has changed, either through local setting or remote instruction.
    /// </summary>
    /// <see cref="OnMuteStateChangedDelegate"/>
    public event OnMuteStateChangedDelegate MuteStateChangedEvent;

    /// <summary name="UserData">
    /// Property for communicating local user data changes to be sent to HiFi Spatial Audio Service.
    /// </summary>
    /// <see cref="UserDataWrapper"/>
    public UserDataWrapper UserData { get; internal set; }

    /// <summary name="InputAudioDeviceName">
    /// Property for specifying the input audio device.
    /// </summary>
    public string InputAudioDeviceName {
        set {
            // verify we've been given a valid device name
            var inputs = Microphone.devices;
            for (int i = 0; i < inputs.Length; ++i) {
                if (inputs[i] == value) {
                    _inputDeviceName = value;
                    if (_raviSession) {
                        _raviSession.InputAudioDeviceName = _inputDeviceName;
                    }
                    break;
                }
            }
        }
        get { return _inputDeviceName; }
    }
    string _inputDeviceName = "unknown";

    HiFiMixerInfo _mixerInfo;
    Dictionary<string, IncomingAudioAPIData> _peerDataMap; // "peer key"-->IncomingAudioAPIData
    Dictionary<string, string> _peerKeyMap; // hashdVisitId-->"peer key"
    SortedSet<string> _changedPeerKeys;
    SortedSet<string> _deletedVisitIds;

    OutgoingAudioAPIData _lastUserData;

    MuteState _muteState;

    byte[] _uncompressedData;
    bool _stateHasChanged = false;
    bool _peerDataHasChanged = false;
    bool _peersHaveDisconnected = false;
    bool _neverConnected = true;
    bool _terminateNextUpdate = false;

    public HiFiCommunicator() {
        _userDataUpdateExpiry = 0;
        _connectingExpiry = 0;
        _reconnectingExpiry = 0;
        _attemptExpiry = THE_DISTANT_FUTURE;
        _nextAttempt = THE_DISTANT_FUTURE;
    }

    void Awake() {
        RaviUtil.InitializeWebRTC();
        _muteState = new MuteState();

        // default init to first available mic (if available)
        // when _inputDeviceName has not been set by external logic.
        if (_inputDeviceName == "unknown") {
            var devices = Microphone.devices;
            if (devices.Length > 0) {
                _inputDeviceName = devices[0];
            }
        }

        const int MAX_UNCOMPRESSED_BUFFER_SIZE = 65536;
        _uncompressedData = new byte[MAX_UNCOMPRESSED_BUFFER_SIZE];

        ConnectionState = AudionetConnectionState.Disconnected;
        ConnectionConfig = new HiFiConnectionAndTimeoutConfig();

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
    }

    void FixedUpdate() {
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

        // retry/reconnection logic here
        long now = DateTimeOffset.Now.Ticks;
        if (ConnectionState == AudionetConnectionState.Unavailable) {
            // the destination server is full --> fallback to retry/reconnect strategy
            if (_neverConnected) {
                // we have not yet successfully connected
                if (ConnectionConfig.AutoRetryConnection && now < _connectingExpiry) {
                    // and we're configured to retry
                    _attemptExpiry = 0;
                    _nextAttempt = THE_DISTANT_FUTURE;
                    UpdateState(AudionetConnectionState.Connecting);
                }
            } else if (ConnectionConfig.AutoReconnect) {
                // we've successfully connected at least once before
                // and we're configured to try to reconnect
                if (_reconnectingExpiry == 0) {
                    // this is a fresh reconnection attempt
                    _reconnectingExpiry = now + ConnectionConfig.ReconnectionTimeout * TICKS_PER_SECOND;
                    _attemptExpiry = 0;
                    UpdateState(AudionetConnectionState.Reconnecting);
                } else if (now > _reconnectingExpiry) {
                    // we're giving up reconnecting
                    // disable future attempts and fail
                    _attemptExpiry = THE_DISTANT_FUTURE;
                    _nextAttempt = THE_DISTANT_FUTURE;
                    UpdateState(AudionetConnectionState.Failed);
                } else {
                    // this series of reconnection attempts have not yet expired
                    _attemptExpiry = 0;
                    _nextAttempt = THE_DISTANT_FUTURE;
                    UpdateState(AudionetConnectionState.Reconnecting);
                }
            }
        }
        if (ConnectionState == AudionetConnectionState.Connecting
                || ConnectionState == AudionetConnectionState.Reconnecting)
        {
            // update next attempt

            // The first two checks are for total timeouts of connect/reconnect
            if (ConnectionState == AudionetConnectionState.Connecting && now > _connectingExpiry) {
                // we're giving up retrying
                // disable future attempts and go straight to true fail
                _attemptExpiry = THE_DISTANT_FUTURE;
                _nextAttempt = THE_DISTANT_FUTURE;
                UpdateState(AudionetConnectionState.Failed);
                DestroySession();
            } else if (ConnectionState == AudionetConnectionState.Reconnecting && now > _reconnectingExpiry) {
                // we're giving up reconnecting
                // disable future attempts and go straight to true fail
                _attemptExpiry = THE_DISTANT_FUTURE;
                _nextAttempt = THE_DISTANT_FUTURE;
                UpdateState(AudionetConnectionState.Failed);
                DestroySession();
            } else {
                // The last two checks are for starting/stopping each attempt.
                // In the event of constant failures the logic is designed oscillate between
                // one expiry and then the other until the total timeout expires above.
                if (now > _attemptExpiry) {
                    // stop this attempt
                    _attemptExpiry = THE_DISTANT_FUTURE;
                    _nextAttempt = now + ConnectionConfig.ConnectionDelayMs * TICKS_PER_MSEC;
                    DestroySession();
                } else if (now > _nextAttempt
                        && !(ConnectionState == AudionetConnectionState.Connecting && !ConnectionConfig.AutoRetryConnection)
                        && !(ConnectionState == AudionetConnectionState.Reconnecting && !ConnectionConfig.AutoReconnect))
                {
                    // start a new attempt
                    _attemptExpiry = now + ConnectionConfig.ConnectionTimeoutMs * TICKS_PER_MSEC;
                    _nextAttempt = THE_DISTANT_FUTURE;
                    // In theory: it should be impossible to reach this context with a valid _connection
                    // but just in case: we call DestroySession() right before CreateSession().
                    DestroySession();
                    CreateSession();
                }
            }
        }

        if (_terminateNextUpdate) {
            _terminateNextUpdate = false;
            // set _reconnectingExpiry to expired non-zero value to prevent reconnection attempts
            _reconnectingExpiry = 1;
            DisconnectFromHiFiAPIServer();
            return;
        }

        if (ConnectionState != AudionetConnectionState.Connected) {
            return;
        }

        if (!UserData.hasChanged) {
            return;
        }

        if (now < _userDataUpdateExpiry) {
            // do nothing yet
            return;
        }
        _userDataUpdateExpiry += _userDataUpdatePeriod * TICKS_PER_MSEC;
        if (_userDataUpdateExpiry < now) {
            _userDataUpdateExpiry = now + _userDataUpdatePeriod * TICKS_PER_MSEC;
        }

        UserData.hasChanged = false;
        TransmitHiFiAudioAPIDataToServer();
    }

    void ClearPeerData() {
        lock (_peerDataMap) {
            foreach (var item in _peerKeyMap) {
                _deletedVisitIds.Add(item.Key);
            }
            _changedPeerKeys.Clear();
            _peerKeyMap.Clear();
            _peerDataMap.Clear();
            _peerDataHasChanged = false;
            _peersHaveDisconnected = true;
        }
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
        RaviUtil.DisposeWebRTC();
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
            DestroySession();

            long now = DateTimeOffset.Now.Ticks;
            if (_connectingExpiry == 0) {
                // this is our first attempt to connect
                // and this is the only place where we set _connectingExpiry non-zero
                long try_period = 5; // seconds
                if (ConnectionConfig.AutoRetryConnection) {
                    if (ConnectionConfig.RetryConnectionTimeout > try_period) {
                        try_period = ConnectionConfig.RetryConnectionTimeout;
                    }
                }
                _connectingExpiry = now + try_period * TICKS_PER_SECOND;
                Log.UncommonEvent(this, "ConnectToHiFiAudioAPIServer _connectingExpiry={0}", _connectingExpiry);
                UpdateState(AudionetConnectionState.Connecting);
            } else {
                _reconnectingExpiry = now + ConnectionConfig.ReconnectionTimeout * TICKS_PER_SECOND;
                UpdateState(AudionetConnectionState.Reconnecting);
            }

            CreateSession();
        }
    }

    /// <summary>
    /// Disconnect from HiFi Spatial Audio Service.
    /// </summary>
    public void DisconnectFromHiFiAPIServer() {
        if (ConnectionState != AudionetConnectionState.Disconnecting
                && ConnectionState != AudionetConnectionState.Disconnected)
        {
            if (_raviSession != null) {
                // we close the _raviSession and expect it to eventually change state
                // and when we get the callback we'll change our own state to Disconnected
                // TODO?: add timout just in case _raviSession doesn't change state?
                UpdateState(AudionetConnectionState.Disconnecting);
                _raviSession.Close();
            } else {
                UpdateState(AudionetConnectionState.Disconnected);
            }
        }
    }

    /// <summary>
    /// Adjust the volume of a peer's audio for this user.
    /// </summary>
    /// <param name="visitIdHash">Unique string for target user.</param>
    /// <param name="gain">Float value in range [0,1].</param>
    /// <returns>True if request was sent to HiFi Spatial Audio Service.</returns>
    public bool SetOtherUserGainForThisConnection(string visitIdHash, float gain) {
        Log.Debug(this, "SendOtherUserGainForThisConnection id='{0}' gain={1}", visitIdHash, gain);
        if (ConnectionState == AudionetConnectionState.Connected) {
            JSONNode payload = new JSONObject();
            payload["visit_id_hash"] = visitIdHash;
            payload["gain"] = gain;
            bool success = _raviSession.CommandController.SendCommand("audionet.personal_volume_adjust", payload);
            if (!success) {
                Log.Warning(this, "SEND audionet.personal_volume_adjust failed");
            }
            return success;
        }
        return false;
    }

    void RemoveRaviSessionHandlers() {
        _raviSession.CommandController.RemoveCommandHandler("audionet.init");
        _raviSession.CommandController.RemoveCommandHandler("audionet.personal_volume_adjust");
        _raviSession.CommandController.BinaryCommandHandler = null;
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

    void FailOrScheduleNextAttempt() {
        long now = DateTimeOffset.Now.Ticks;
        if (ConnectionState == AudionetConnectionState.Disconnected) {
            // nothing to do
        } else if (ConnectionState == AudionetConnectionState.Disconnecting) {
            // we probably initiated a shutdown and it has happened
            UpdateState(AudionetConnectionState.Disconnected);
        } else if (_neverConnected) {
            // we have not yet successfully connected
            if (now > _connectingExpiry) {
                // we're giving up retrying
                // disable future attempts and fail
                _attemptExpiry = THE_DISTANT_FUTURE;
                _nextAttempt = THE_DISTANT_FUTURE;
                UpdateState(AudionetConnectionState.Failed);
            } else {
                // we're not done trying so we set an expiry to try again
                if (now < _attemptExpiry) {
                    // we clear _attemptExpiry
                    // which will cause the retry logic to start again at next update()
                    _attemptExpiry = 0;
                }
                UpdateState(AudionetConnectionState.Connecting);
            }
        } else if (ConnectionConfig.AutoReconnect) {
            // we've successfully connected at least once before
            // and we're configured to try to reconnect
            if (_reconnectingExpiry == 0) {
                // this is a fresh reconnection attempt
                _reconnectingExpiry = now + ConnectionConfig.ReconnectionTimeout * TICKS_PER_SECOND;
                _attemptExpiry = 0;
                UpdateState(AudionetConnectionState.Reconnecting);
            } else {
                if (now > _reconnectingExpiry) {
                    // we're giving up reconnecting
                    // disable future attempts and fail
                    _attemptExpiry = THE_DISTANT_FUTURE;
                    _nextAttempt = THE_DISTANT_FUTURE;
                    UpdateState(AudionetConnectionState.Failed);
                } else {
                    // this series of reconnection attempts have not yet expired
                    _attemptExpiry = 0;
                    // we should already be in Reconnecting state
                    // but set it again Reconnecting here, just in case
                    // (it is a no-op if already there)
                    UpdateState(AudionetConnectionState.Reconnecting);
                }
            }
        } else {
            // we're not configured to reconnect
            // disable future attempts and fail
            _attemptExpiry = THE_DISTANT_FUTURE;
            _nextAttempt = THE_DISTANT_FUTURE;
            UpdateState(AudionetConnectionState.Failed);
        }
    }

    void DestroySession() {
        if (_raviSession != null) {
            //Log.UncommonEvent(this, "DestroySession");
            _raviSession.CommandController.RemoveCommandHandler("audionet.init");
            _raviSession.CommandController.RemoveCommandHandler("audionet.personal_volume_adjust");
            _raviSession.CommandController.BinaryCommandHandler = null;
            _raviSession.SessionStateChangedEvent -= OnRaviSessionStateChanged;
            Destroy(_raviSession);
            _raviSession = null;
        }
    }

    void CreateSession() {
        //Log.UncommonEvent(this, "CreateSession");
        _raviSession = gameObject.AddComponent<RaviSession>() as RaviSession;
        _raviSession.InputAudioDeviceName = _inputDeviceName;
        _raviSession.SessionStateChangedEvent += OnRaviSessionStateChanged;

        SanityCheckSignalingServiceUrl();

        // _raviSession.Connect expects the the full URL with JWT token on the end
        string signalUrl = SignalingServiceUrl + "?token=" + JWT;
        _raviSession.Connect(signalUrl);

        // add command handlers
        _raviSession.CommandController.AddCommandHandler("audionet.init", HandleAudionetInit);
        _raviSession.CommandController.AddCommandHandler("audionet.personal_volume_adjust", HandlePersonalVolumeAdjust);
        _raviSession.CommandController.BinaryCommandHandler = HandleAudionetBinaryData;
    }

    void UpdateState(AudionetConnectionState newState) {
        if (ConnectionState != newState) {
            switch (newState) {
                case AudionetConnectionState.Connected:
                    _neverConnected = false;
                    if (ConnectionState == AudionetConnectionState.Reconnecting) {
                        ClearPeerData();
                    }
                    break;
                case AudionetConnectionState.Reconnecting:
                    // Note: we don't ClearPeerData() here.  Instead we wait until the last minute
                    // (e.g. when transitioning from Reconnecting to Connected).
                    break;
                case AudionetConnectionState.Failed:
                    _raviSession?.Close();
                    ClearPeerData();
                    break;
                case AudionetConnectionState.Disconnected:
                    ClearPeerData();
                    // reset these to allow for fresh connection logic
                    _connectingExpiry = 0;
                    _neverConnected = true;
                    break;
                default:
                    break;
            }
            Log.UncommonEvent(this, "UpdateState: '{0}'-->'{1}'", ConnectionState, newState);
            ConnectionState = newState;
            // fire the event later when we're definitely on main thread
            _stateHasChanged = true;
        }
    }

    bool SendAudionetInit() {
        Log.Debug(this, "SendAudionetInit");
        if (_raviSession != null && _raviSession.CommandController != null) {
            JSONNode payload = new JSONObject();
            payload["primary"] = true;
            payload["visit_id"] = _raviSession.SessionId;
            payload["session"] = _raviSession.SessionId;
            payload["streaming_scope"] = UserDataScopeStrings[(int) UserDataStreamingScope];
            // stereo upload to HiFi Spatial Audio Service is an experimental feature
            // and is not supported in Unity yet.
            bool INPUT_AUDIO_IS_STEREO = false;
            payload["is_input_stream_stereo"] = INPUT_AUDIO_IS_STEREO;

            Log.UncommonEvent(this, "SEND audionet.init");
            bool success = _raviSession.CommandController.SendCommand("audionet.init", payload);
            if (!success) {
                Log.Warning(this, "SEND audionet.init failed");
                UpdateState(AudionetConnectionState.Failed);
            }
            return success;
        }
        Log.Error(this, "SendAudionetInit failed for null _raviSession or CommandController");
        return false;
    }

    bool TransmitHiFiAudioAPIDataToServer() {
        AudioAPIDataChanges changes = _lastUserData.ApplyAndGetChanges(UserData.data);
        if (!changes.IsEmpty()) {
            return _raviSession.CommandController.SendInput(changes.ToWireFormattedJsonString());
        }
        // although we didn't send anything, consider this success
        return true;
    }

    void HandleAudionetInit(string msg) {
        Log.UncommonEvent(this, "HandleAudionetInit RECV audionet.init response msg='{0}'", msg);
        try {
            if (ConnectionState == AudionetConnectionState.Connecting
                    || ConnectionState == AudionetConnectionState.Reconnecting)
            {
                JSONNode obj = JSONNode.Parse(msg);
                _mixerInfo.buildNumber = obj["build_number"];
                _mixerInfo.buildType = obj["build_type"];
                _mixerInfo.buildVersion = obj["build_version"];
                // Note: _mixerInfo.visitIdHash can be used to figure out which of the "peers" data
                // corresponds to our current HiFiSession when using UserDataScope=All
                _mixerInfo.visitIdHash = obj["visit_id_hash"];
                _mixerInfo.userId = obj["user_id"];

                bool success = obj["success"];
                if (success) {
                    _mixerInfo.visitId = _raviSession.SessionId;
                    UpdateState(AudionetConnectionState.Connected);
                    _nextAttempt = THE_DISTANT_FUTURE;
                    _reconnectingExpiry = 0;
                } else {
                    FailOrScheduleNextAttempt();
                }
            } else {
                Log.Warning(this, "HandleAudionetInit RECV audionet.init response with unexpected connectionState='{0}'", ConnectionState);
                FailOrScheduleNextAttempt();
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
        // The json_data is expected to look something like this:
        //
        // json_data = {
        //   "peers":
        //     {"2": {IncomingUserData_InJsonStringFormat},
        //      "9": {IncomingUserData_InJsonStringFormat}
        //       ...
        //     },
        //   "deleted_visit_ids":
        //     [ "dead7", "beef3", "fade1" ],
        //   "instructions":
        //     [ ["mute",true],
        //       ["terminate","{\"reason\":\"kick\"}"]]
        //  }
        lock (_peerDataMap) {
            try {
                JSONNode obj = JSONNode.Parse(text);
                // The "peers" entry is a map of <key, IncomingPeerData> pairs.
                // The key will be used to identify all future updates... except
                // the delete which is identified by the visidIdHash inside the
                // "deleted_visit_ids" entry (which really should have been called
                // "deleted_visit_id_hashes" but oh well).  Therefore when adding
                // new peers we must extract the visidIdHash and store it in a
                // hash->key lookup map for later.
                //
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
                // The "deleted_visit_ids" entry is an array of hashedVisitIds of peers that have left
                // the service from this user's perspective.  We use hashedVisitID as a key to _peerKeyMap
                // which will give us the actual key to _peerDataMap.
                //
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
                // The "instructions" entry is an array whose elements are arrays with the
                // format: [instruction_name, instruction_params...]
                // For example: the server may ask the User to "mute" or to "terminate".
                //
                if (obj.HasKey("instructions")) {
                    JSONNode instructions = obj["instructions"];
                    foreach (JSONNode instruction in instructions) {
                        HandleInstruction(instruction);
                    }
                }
            } catch (Exception e) {
                Log.Error(this, "HandleAudionetBinaryData failed to parse RaviSessionBinaryData err='{0}'", e.Message);
            }
        }
    }

    void HandleInstruction(JSONNode instruction) {
        try {
            // instruction is expected to be an array: [ "name", param0, param1, ... ]
            // at the moment all instruction types expect one parameter
            //     ["mute",true]
            //     ["terminate","{\"reason\":\"kick\"}"]]
            //Log.UncommonEvent(this, "HandleInstruction instruction='{0}'", instruction.ToString());
            string name = instruction[0];
            if (name == "mute") {
                bool mute = instruction[1];
                Log.Warning(this, "HandleInstruction mute='{0}'", mute);
                bool mute_state_changed = false;
                MuteState mute_state = new MuteState();
                lock(_muteState) {
                    // Note: just in case there is a mismatch between _muteState
                    // and the actual state of _raviSession's internals we compute
                    // was_muted and is_muted using distinct logic
                    bool was_muted = _muteState.currentMuteValue;
                    bool is_muted = true;
                    if (_raviSession) {
                        is_muted = _raviSession.InputAudioMuted;
                    }
                    if (mute != _muteState.adminPreventsUnmuting) {
                        mute_state_changed = true;
                        _muteState.adminPreventsUnmuting = mute;
                        if (mute) {
                            if (_raviSession) {
                                if (mute != is_muted) {
                                    _raviSession.InputAudioMuted = mute;
                                    is_muted = _raviSession.InputAudioMuted;
                                }
                            }
                        } else {
                            // The server doesn't actually unmute the audio.
                            // All it does is clear the adminPreventsUnmuting bit
                            // so the client App can unmute at its leisure.
                        }
                    }
                    _muteState.success = (mute == is_muted);
                    _muteState.targetMuteValue = mute;
                    _muteState.currentMuteValue = is_muted;
                    if (_muteState.targetMuteValue != mute) {
                        mute_state_changed = true;
                    }
                    if (mute) {
                        _muteState.reason = "server muted";
                    } else {
                        _muteState.reason = "server unmuted";
                    }
                    if (was_muted != _muteState.currentMuteValue || mute_state_changed) {
                        mute_state_changed = true;
                        mute_state = _muteState.DeepCopy();
                    }
                }

                if (mute_state_changed) {
                    MuteStateChangedEvent?.Invoke(mute_state);
                }
            } else if (name == "terminate") {
                string reason = instruction[1];
                Log.Warning(this, "HandleInstruction terminate reason='{0}'", reason);
                _terminateNextUpdate = true;
            }
        } catch (Exception) {
            Log.Warning(this, "HandleInstruction failed to parse instruction msg='{0}'", instruction.ToString());
        }
    }

    void OnRaviSessionStateChanged(RaviSession.SessionState state) {
        Log.UncommonEvent(this, "OnRaviSessionStateChanged state={0} session_state={1}", ConnectionState, state);

        switch (state) {
            case RaviSession.SessionState.Connected:
                // nothing to do yet: we're waiting for ConnectedWithBothDataChannels
                break;
            case RaviSession.SessionState.ConnectedWithBothDataChannels:
                if (ConnectionState == AudionetConnectionState.Connecting
                        || ConnectionState == AudionetConnectionState.Reconnecting)
                {
                    // at this point the Session is ready for us to "login"
                    // so we send the auidionet.init message and when that comes back
                    // we'll transition to Connected
                    bool success = SendAudionetInit();
                    if (!success) {
                        UpdateState(AudionetConnectionState.Failed);
                    }
                }
                break;
            case RaviSession.SessionState.Disconnected:
            case RaviSession.SessionState.Failed:
            case RaviSession.SessionState.Closed:
            case RaviSession.SessionState.Closing:
                if (ConnectionState == AudionetConnectionState.Connecting
                        || ConnectionState == AudionetConnectionState.Connected)
                {
                    FailOrScheduleNextAttempt();
                }
                break;
            case RaviSession.SessionState.Unavailable:
                UpdateState(AudionetConnectionState.Unavailable);
                break;
            default:
                break;
        }
    }
}

} // namespace Ravi


