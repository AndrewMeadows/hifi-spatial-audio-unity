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
    public delegate void OnConnectionStateChangedDelegate(AudionetConnectionState state);
    //public delegate void OnUserDataUpdatedDelegate(ReceivedAudioAPIData[] data);
    //public delegate void OnUsersDisconnectedDelegate(ReceivedAudioAPIData[] data);

    // these events accept listeners
    public event OnConnectionStateChangedDelegate ConnectionStateChangedEvent;
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
    }

    private void Start() {
        Debug.Log("HiFiSession.Start");
    }

    private void Update() {
        if (_stateHasChanged) {
            _stateHasChanged = false;
            ConnectionStateChangedEvent.Invoke(_connectionState);
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
            _stateHasChanged = true;
        }
    }

    private bool SendAudionetInit() {
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
            }
        } catch (Exception e) {
            Debug.Log($"HandleAudionetInit failed to parse message='{msg}' err='{e.Message}'");
        }
    }
}

#if BAR
let commandController = self.RaviSession.getCommandController()
// TODO: Re-implement this init timeout later.
//            let INIT_TIMEOUT_MS = 5000;
//            let initTimeout = setTimeout(() => {
//                this.disconnect();
//                return Promise.reject({
//                    success: false,
//                    error: `Couldn't connect to mixer: Call to \`init\` timed out!`
//                });
//            }, INIT_TIMEOUT_MS);

let audionetInitParams = [
    "primary": true,
    "visit_id": self.RaviSession.getUUID(), // The mixer will hash this randomly-generated UUID, then disseminate it to all clients via `peerData.e`.
    "session": self.RaviSession.getUUID(), // Still required for old mixers. Will eventually go away.
    "streaming_scope": self.userDataStreamingScope.rawValue,
    "is_input_stream_stereo": self._inputAudioMediaStreamIsStereo
] as [String : Any]
let initCommandHandler = RaviCommandHandler(commandName: "audionet.init") { response in
    do {
        if (response == nil) {
            return
        }
        let decoder = JSONDecoder()
        let responseData = try decoder.decode(AudionetInitResponseData.self, from: response!.data(using: .utf8)!)
        self.mixerInfo.connected = true
        self.mixerInfo.buildNumber = responseData.build_number
        self.mixerInfo.buildType = responseData.build_type
        self.mixerInfo.buildVersion = responseData.build_version
        self.mixerInfo.visitIdHash = responseData.visit_id_hash
        self.mixerInfo.unhashedVisitID = self.RaviSession.getUUID()

        let response = AudionetInitResponse(success: true, error: nil, responseData: responseData)

        fulfill(response)
    } catch {
        let response = AudionetInitResponse(success: false, error: "Couldn't parse init response!", responseData: nil)
        reject(NSError(domain: "", code: 1, userInfo: response.dictionary))
    }
}
let initCommand = RaviCommand(commandName: "audionet.init", params: audionetInitParams, commandHandler: initCommandHandler)
_ = commandController.sendCommand(raviCommand: initCommand)
#endif // BAR

} // namespace Ravi


