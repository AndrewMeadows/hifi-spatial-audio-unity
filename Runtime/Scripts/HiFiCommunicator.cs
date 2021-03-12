/**
 * The [[HiFiCommunicator]] component provides the main API for using the High Fidelity Audio Service
 * - `connectToHiFiAudioAPIServer()`: Connect to High Fidelity Audio Server
 * - `disconnectFromHiFiAudioAPIServer()`: Disconnect from High Fidelity Audio Server
 * - `updateUserDataAndTransmit()`: Update the user's data (position, orientation, etc) on the High Fidelity Audio Server
 * - `setInputAudioMediaStream()`: Set a new input audio media stream (for example, when the user's audio input device changes)
 * @packageDocumentation
 */

//using Microsoft.MixedReality.WebRTC;
//using Microsoft.MixedReality.WebRTC.Unity;
//using NativeWebSocket;
using SimpleJSON;
using System;
using System.Collections;
using System.Threading.Tasks;
//using UnityEngine;
//using UnityEngine.Networking;

namespace HiFi {

// The HiFiCommunicator represents a Component on a GameObject
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

    public ConnectionState State {
        get { return _connectionState; }
    }

    public UserDataScope UserDataStreamingScope = UserDataScope.All;
    public string AxisConfigString = "R+X+Y";

    // these delegates are available when adding listeners to corresponding events
    public delegate void OnConnectionStateChangedDelegate(ConnectionState state);
    public delegate void OnUserDataUpdatedDelegate(ReceivedHiFiAudioAPIData[] data);
    public delegate void OnUsersDisconnectedDelegate(ReceivedHiFiAudioAPIData[] data);

    // these events accept listeners
    public event OnConnectionStateChangedDelegate ConnectionStateChangedEvent;
    public event OnUserDataUpdatedDelegate UserDataUpdatedEvent;
    public event OnUsersDisconnectedDelegate UsersDisconnectedEvent;

    private ConnectionState _connectionState = ConnectionState.Disconnected;

    private void Awake() {
    }

    private void Start() {
    }

    private void Update() {
    }

    private void OnDestroy() {
    }

    public void ConnectToHiFiAudioAPIServer() {
    }

    public void DisconnectFromHiFiAudioAPIServer() {
    }

    public void SetInputAudioMediaStream() {
    }

    private void UpdateState(State newState) {
    }
}

} // namespace Ravi


