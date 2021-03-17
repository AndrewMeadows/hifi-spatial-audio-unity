/**
 * The [[HiFiCommunicator]] component provides the main API for using the High Fidelity Audio Service
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

/// <summary>
/// Component for access to HiFi Spatial Audio API
/// </summary>
[AddComponentMenu("Component for access to HiFi Spatial Audio API")]
public class HiFiCommunicator : MonoBehaviour {
    // possible states of the Communicator
    public enum ConnectionState {
        Connected = 0,
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

    /// <summary>
    /// State of connection to HiFi Spatial Audio Service
    /// </summary>
    public ConnectionState State {
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
    /// Unique local user identifier
    /// </summary>
    [Tooltip("Unique local user identifier")]
    public string SessionId;

    /// <summary>
    /// RaviSession
    /// </summary>
    [Tooltip("RaviSession")]
    public Ravi.RaviSession RaviSession;

    //public string AxisConfigString = "R+X+Y";

    // uncomment these when it is time to use them
    // these delegates are available when adding listeners to corresponding events
    public delegate void OnConnectionStateChangedDelegate(ConnectionState state);
    //public delegate void OnUserDataUpdatedDelegate(ReceivedAudioAPIData[] data);
    //public delegate void OnUsersDisconnectedDelegate(ReceivedAudioAPIData[] data);

    // these events accept listeners
    public event OnConnectionStateChangedDelegate ConnectionStateChangedEvent;
    //public event OnUserDataUpdatedDelegate UserDataUpdatedEvent;
    //public event OnUsersDisconnectedDelegate UsersDisconnectedEvent;


    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private bool _stateHasChanged = false;

    private void Awake() {
        Debug.Log("HiFiCommunicator.Awake");
        if (RaviSession == null) {
            CreateRaviSession();
        }
    }

    private void Start() {
        Debug.Log("HiFiCommunicator.Start");
    }

    private void Update() {
        if (_stateHasChanged) {
            _stateHasChanged = false;
            ConnectionStateChangedEvent.Invoke(_connectionState);
        }
    }

    private void OnDestroy() {
    }

    public void ConnectToHiFiAudioAPIServer() {
        Debug.Log("HiFiCommunicator.ConnectToHiFiAudioAPIServer");
        RaviSession.SignalUrl = SignalUrl;
        RaviSession.Open();
        // TODO: tie onto RaviSession state changes
        // Also: need to addCommands
    }

    public void DisconnectFromHiFiAudioAPIServer() {
        // TODO: implement this
    }

    public void SetInputAudioMediaStream() {
    }

    private void CreateRaviSession() {
        Debug.Log("HiFiCommunicator.CreateRaviSession");
        RaviSession = gameObject.AddComponent<Ravi.RaviSession>() as Ravi.RaviSession;
    }

    private void UpdateState(ConnectionState newState) {
        if (_connectionState != newState) {
            _connectionState = newState;
            // fire the event later when we're on main thread
            _stateHasChanged = true;
        }
    }
}

} // namespace Ravi


