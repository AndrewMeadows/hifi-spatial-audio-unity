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

[Serializable]
public class RaviSessionPeerData {
    string J;
    string e;
    double? x;
    double? y;
    double? z;
    double? W;
    double? X;
    double? Y;
    double? Z;
    float? g;
    float? v;
}

[Serializable]
public class RaviSessionBinaryData {
    string[] deleted_visit_ids;
    RaviSessionPeerData[] peers;
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
    /// Url to Signal service
    /// </summary>
    [Tooltip("Signalurl")]
    public string SignalUrl = HiFi.Constants.DEFAULT_SIGNAL_URL;

    /// <summary>
    /// RaviSession
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
        RaviSession.SignalUrl = SignalUrl;
        RaviSession.Open();
        // TODO: tie onto RaviSession state changes
        //
        // add all command handlers
        RaviSession.CommandController.AddHandler("audionet.init", HandleAudionetInit);
        RaviSession.CommandController.BinaryHandler = HandleRaviSessionBinaryData;
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

    private void UpdateState(AudionetConnectionState newState) {
        if (_connectionState != newState) {
            _connectionState = newState;
            // fire the event later when we're on main thread
            Debug.Log($"HiFiSession.UpdateState: '{_connectionState}' --> '{newState}'");
            _stateHasChanged = true;
        }
    }

    private bool SendAudionetInit() {
        Debug.Log("HiFiSession.SendAudionetInit");
        if (RaviSession != null && RaviSession.CommandController != null) {
            if (_connectionState == AudionetConnectionState.Failed) {
                UpdateState(AudionetConnectionState.Disconnected);
            }
            JSONNode obj = new JSONObject();
            obj["primary"] = true;
            obj["visit_id"] = RaviSession.SessionId;
            obj["session"] = RaviSession.SessionId;
            obj["streaming_scope"] = UserDataScopeStrings[(int) UserDataStreamingScope];
            obj["is_input_stream_stereo"] = InputAudioIsStereo;
            if (_connectionState == AudionetConnectionState.Disconnected) {
                UpdateState(AudionetConnectionState.Connecting);
            }
            Debug.Log("HiFiSession SEND audionet.init");
            bool success = RaviSession.CommandController.SendCommand("audionet.init", obj.ToString());
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
                Debug.Log($"HandleAudionetInit RECV audionet.init response but connectionState='{_connectionState}'");
            }
        } catch (Exception e) {
            Debug.Log($"HandleAudionetInit failed to parse message='{msg}' err='{e.Message}'");
        }
    }

    private void HandleRaviSessionBinaryData(byte[] data) {
        Debug.Log($"HandleRaviSessionBinaryData data.Length={data.Length}");
        // 'data' may be gzipped so we first try to decompress it
        const int MAX_UNCOMPRESSED_BUFFER_SIZE = 1024;
        byte[] uncompressedData = new byte[MAX_UNCOMPRESSED_BUFFER_SIZE];
        try {
            using(MemoryStream zipped = new MemoryStream(data)) {
                using (MemoryStream unzipped = new MemoryStream(uncompressedData)) {
                    using(GZipStream unzipper = new GZipStream(zipped, CompressionMode.Decompress)) {
                        unzipper.CopyTo(unzipped);
                    }
                }
            }
        } catch (Exception) {
            // decompression failed
            uncompressedData = data;
        }
        // TODO: figure out if this is being called in the main thread or not
        Debug.Log($"foo = {uncompressedData.Length}");

         string text = System.Text.Encoding.UTF8.GetString(uncompressedData);
        RaviSessionBinaryData sessionData = JsonUtility.FromJson<RaviSessionBinaryData>(text);

        // BOOKMARK: finish implementing this
    }

    private void TransmitHiFiAudioAPIData(HiFi.OutgoingAudioAPIData newData, HiFi.OutgoingAudioAPIData oldData) {
        Debug.Log("TODO: implement this");
    }

    public void OnRaviSessionStateChanged(Ravi.RaviSession.SessionState state) {
        Debug.Log($"HiFiSession.OnRaviSessionStateChanged state='{state}'");
        if (state == Ravi.RaviSession.SessionState.Connected) {
            SendAudionetInit();
        }
    }
}

} // namespace Ravi


