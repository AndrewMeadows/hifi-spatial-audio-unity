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

public class RaviSessionPeerData {
    //public string J; // JWT user ID
    public string e; // hashedVisitId
    public float v; // volume

    public float x;
    public float y;
    public float z;
    public float W;
    public float X;
    public float Y;
    public float Z;

    public void LoadDefaults() {
        //J = "";
        e = "";
        v = 0.0f;
        x = 0.0f;
        y = 0.0f;
        z = 0.0f;
        W = 1.0f;
        X = 0.0f;
        Y = 0.0f;
        Z = 0.0f;
    }

    public void UpdateFromJson(JSONNode obj) {
        try {
            e = obj["e"].Value;
        } catch (KeyNotFoundException) {
        }
        try {
            v = obj["v"].AsFloat;
        } catch (KeyNotFoundException) {
        }
        try {
            x = obj["x"].AsFloat;
        } catch (KeyNotFoundException) {
        }
        try {
            y = obj["y"].AsFloat;
        } catch (KeyNotFoundException) {
        }
        try {
            z = obj["z"].AsFloat;
        } catch (KeyNotFoundException) {
        }
        try {
            W = obj["W"].AsFloat;
        } catch (KeyNotFoundException) {
        }
        try {
            X = obj["X"].AsFloat;
        } catch (KeyNotFoundException) {
        }
        try {
            Y = obj["Y"].AsFloat;
        } catch (KeyNotFoundException) {
        }
        try {
            Z = obj["Z"].AsFloat;
        } catch (KeyNotFoundException) {
        }
    }
}

public class RaviSessionBinaryData {
    public string[] deleted_visit_ids;
    public RaviSessionPeerData[] peers;
}

/// <summary>
/// Component for access to HiFi Spatial Audio API
/// </summary>
[AddComponentMenu("Component for access to HiFi Spatial Audio API")]
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

    //public string AxisConfigString = "R+X+Y";

    // uncomment these when it is time to use them
    // these delegates are available when adding listeners to corresponding events
    public delegate void ConnectionStateChangedDelegate(AudionetConnectionState state);
    //public delegate void OnUserDataUpdatedDelegate(ReceivedAudioAPIData[] data);
    //public delegate void OnUsersDisconnectedDelegate(ReceivedAudioAPIData[] data);

    // these events accept listeners
    public event ConnectionStateChangedDelegate ConnectionStateChangedEvent;
    //public event OnUserDataUpdatedDelegate UserDataUpdatedEvent;
    //public event OnUsersDisconnectedDelegate UsersDisconnectedEvent;

    public bool InputAudioIsStereo = false;

    private AudionetConnectionState _connectionState = AudionetConnectionState.Disconnected;
    private bool _stateHasChanged = false;
    private HiFiMixerInfo _mixerInfo;
    private Dictionary<string, RaviSessionPeerData> _peerDataMap;

    private void Awake() {
        Debug.Log("HiFiSession.Awake");
        if (RaviSession == null) {
            CreateRaviSession();
        }
        RaviSession.SessionStateChangedEvent += OnRaviSessionStateChanged;
    }

    private void Start() {
        Debug.Log("HiFiSession.Start");
    }

    private void Update() {
        if (_stateHasChanged) {
            // we waited to Invoke this on the main thread
            _stateHasChanged = false;
            ConnectionStateChangedEvent?.Invoke(_connectionState);
        }
    }

    private void OnDestroy() {
    }

    public void Connect() {
        Debug.Log("HiFiSession.Connect");
        SanityCheckSignalingServiceUrl();
        // The HiFi SignalingServiceUrl expects the JWT token on the end
        string signalUrl = SignalingServiceUrl + "?token=" + JWT;

        RaviSession.Open(signalUrl);

        // add command handlers
        RaviSession.CommandController.AddHandler("audionet.init", HandleAudionetInit);
        RaviSession.CommandController.BinaryHandler = HandleAudionetBinaryData;
    }

    public void Disconnect() {
        // TODO: implement this
        RaviSession.Close();
    }

    public void SetInputAudioMediaStream() {
        // TODO?: implement this?
    }

    private void CreateRaviSession() {
        Debug.Log("HiFiSession.CreateRaviSession");
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
            Debug.Log($"HiFiSession.SanityCheckSignalingServiceUrl '{originalUrl}' --> '{SignalingServiceUrl}'");
        }
    }

    private void UpdateState(AudionetConnectionState newState) {
        if (_connectionState != newState) {
            Debug.Log($"HiFiSession.UpdateState: '{_connectionState}' --> '{newState}'");
            _connectionState = newState;
            // fire the event later when we're on main thread
            _stateHasChanged = true;
        }
    }

    private bool SendAudionetInit() {
        Debug.Log("HiFiSession.SendAudionetInit");
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
            Debug.Log("HiFiSession SEND audionet.init");
            //bool success = RaviSession.CommandController.SendCommand("audionet.init", payload.ToString());
            bool success = RaviSession.CommandController.SendCommand("audionet.init", payload);
            if (!success) {
                UpdateState(AudionetConnectionState.Failed);
            }
        }
        return false;
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
                Debug.Log($"HiFiSession.HandleAudionetInit RECV audionet.init response with unexpected connectionState='{_connectionState}'");
            }
        } catch (Exception e) {
            Debug.Log($"HiFiSession.HandleAudionetInit failed to parse message='{msg}' err='{e.Message}'");
        }
    }

    private void HandleAudionetBinaryData(byte[] data) {
        Debug.Log($"HiFiSession.HandleAudionetBinaryData data.Length={data.Length}");
        // 'data' may be gzipped so we first try to decompress it
        const int MAX_UNCOMPRESSED_BUFFER_SIZE = 1024;
        byte[] uncompressedData = new byte[MAX_UNCOMPRESSED_BUFFER_SIZE];
        //byte[] uncompressedData = new byte[];
        try {
            using(MemoryStream zipped = new MemoryStream(data)) {
                using(GZipStream unzipper = new GZipStream(zipped, CompressionMode.Decompress)) {
                    int numBytes = unzipper.Read(uncompressedData, 0, uncompressedData.Length);
                    Debug.Log($"HiFiSession.HandleAudionetBinaryData numBytesUncompressed={numBytes}");
                    string uncompressedText = System.Text.Encoding.UTF8.GetString(uncompressedData, 0,  numBytes);
                    Debug.Log($"HiFiSession.HandleAudionetBinaryData uncompressedText='{uncompressedText}'");
                }
            }
        } catch (Exception) {
            // decompression failed
            uncompressedData = data;
        }
        // TODO: figure out if this is being called in the main thread or not
        Debug.Log($"HiFiSession.HandleAudionetBinaryData uncompressedData.Length={uncompressedData.Length}");

        string text = System.Text.Encoding.UTF8.GetString(uncompressedData);
        try {
            //RaviSessionBinaryData sessionData = JsonUtility.FromJson<RaviSessionBinaryData>(text);
            JSONNode obj = JSONNode.Parse(text);
            if (obj.HasKey("peers")) {
                JSONNode peers = obj["peers"];
                foreach (string key in peers.Keys) {
                    JSONNode peerInfo = peers[key];
                    try {
                        _peerDataMap[key].UpdateFromJson(peerInfo);
                    } catch (KeyNotFoundException) {
                        RaviSessionPeerData d = new RaviSessionPeerData();
                        d.LoadDefaults();
                        d.UpdateFromJson(peerInfo);
                        _peerDataMap.Add(key, d);
                    }
                }
            }
            if (obj.HasKey("deleted_visit_ids")) {
                // TODO: implement this
            }
            //Debug.Log($"HiFiSession.HandleAudionetBinaryData deleted_visit_ids.Length={obj.deleted_visit_ids.Length} peers.Length={sessionData.peers.Length}");
        } catch (Exception) {
            Debug.Log("HiFiSession.HandleAudionetBinaryData failed to parse RaviSessionBinaryData");
        }

        // BOOKMARK: finish implementing this
    }

    private void TransmitHiFiAudioAPIData(HiFi.OutgoingAudioAPIData newData, HiFi.OutgoingAudioAPIData oldData) {
        Debug.Log("RaviSession.TransmitHiFiAudioAPIData TODO: implement this");
    }

    public void OnRaviSessionStateChanged(Ravi.RaviSession.SessionState state) {
        Debug.Log($"HiFiSession.OnRaviSessionStateChanged state='{state}'");
        if (state == Ravi.RaviSession.SessionState.Connected) {
            SendAudionetInit();
        }
    }
}

} // namespace Ravi


