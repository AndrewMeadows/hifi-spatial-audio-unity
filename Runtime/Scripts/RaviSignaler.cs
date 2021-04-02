// RaviSignaler.cs
//
using NativeWebSocket;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
//using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

namespace Ravi {

public class RaviSignaler : MonoBehaviour {
    public enum SignalState {
        New,
        Opening,
        Connecting,
        Signaling,
        Stable,
        Disconnected,
        Failed,
        Closed
    }

    public delegate void SignalStateChangeDelegate(SignalState state);

    public string WebSocketUrl;
    public string LocalPeerId; // Uuid string in 4211 format
    public SignalState State { get; internal set; }
    public RTCPeerConnection PeerConnection;
    public event SignalStateChangeDelegate SignalStateChangedEvent; // DANGER: not necessarily fired on main thread!

    private WebSocket _webSocket;
    private bool _disposeOnDestroy = false;

    private RTCAnswerOptions _answerOptions = new RTCAnswerOptions {
        iceRestart = false,
    };

    public RaviSignaler() {
        State = SignalState.New;
    }

    void Awake() {
        Debug.Log("RaviSignaler.Awake");
        _disposeOnDestroy = RaviUtil.InitializeWebRTC();
        if (!RaviMainThreadDispatcher.Exists()) {
            RaviMainThreadDispatcher dispatcher = gameObject.AddComponent<RaviMainThreadDispatcher>() as RaviMainThreadDispatcher;
        }
    }

    void Start() {
        Debug.Log("RaviSignaler.Start");
    }

    void Update() {
        if (_webSocket != null) {
            if (_webSocket.State == WebSocketState.Open) {
                #if !UNITY_WEBGL || UNITY_EDITOR
                _webSocket.DispatchMessageQueue();
                #endif
            } else if (_webSocket.State == WebSocketState.Closed) {
            }
        }
    }

    void UpdateState(SignalState newState) {
        // DANGER: not necessarily fired on main thread!
        if (newState != State) {
            Debug.Log($"RaviSignaler.UpdateState {State}-->{newState}");
            State = newState;
            this.SignalStateChangedEvent?.Invoke(newState);
        }
    }

    void OnDestroy() {
        if (_disposeOnDestroy) {
            RaviUtil.DisposeWebRTC();
            _disposeOnDestroy = false;
        }
    }

    void SendSignal(string text) {
        Debug.Log($"SEND signal='{text}'");
        _webSocket.SendText(text);
    }

    void LatchOntoPeerConnection() {
        Debug.Log("RaviSignaler.LatchOntoPeerConnection");
        // these are the public delegates available for RTCPeerConnection:
        //
        // void DelegateOnIceCandidate(RTCIceCandidate candidate);
        // void DelegateOnIceConnectionChange(RTCIceConnectionState state);
        // void DelegateOnIceGatheringStateChange(RTCIceGatheringState state);
        // void DelegateOnNegotiationNeeded();
        // void DelegateOnTrack(RTCTrackEvent e);
        // void DelegateSetSessionDescSuccess();
        // void DelegateSetSessionDescFailure(RTCError error);
        //
        // and these are the delegate of interest to this signaler:
        PeerConnection.OnIceConnectionChange = HandleIceConnectionChange;
        PeerConnection.OnIceGatheringStateChange = HandleIceGatheringStateChange;
        PeerConnection.OnIceCandidate = HandleIceCandidate;
        PeerConnection.OnNegotiationNeeded = OnNegotiationNeeded;
    }

    public bool Connect(string url) {
        Debug.Log($"RaviSignaler.Connect url={url}");
        if (State == SignalState.Failed) {
            UpdateState(SignalState.Closed);
        }
        if (State == SignalState.New || State == SignalState.Closed) {
            WebSocketUrl = url;

            bool success = OpenWebSocket();
            if (success) {
                Debug.Log("RaviSignaler.Connect successful OpenWebSocket");
                if (PeerConnection == null) {
                    Debug.Log("RaviSignaler.Connect create PeerConnection");
                    // we were not given a PeerConnection from external logic
                    // so we create our own and external logic must reference it
                    RTCConfiguration config = default;
                    config.iceServers = new RTCIceServer[] {
                        new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } }
                    };
                    Debug.Log("RaviSignaler.Connect create PeerConnection...");
                    PeerConnection = new RTCPeerConnection(ref config);
                }
                LatchOntoPeerConnection();
            } else {
                Debug.Log("RaviSignaler.Connect failed to OpenWebSocket");
                UpdateState(SignalState.Failed);
            }
            return success;
        }
        return false;
    }

    bool OpenWebSocket() {
        Debug.Log("RaviSignaler.OpenWebSocket");
        UpdateState(SignalState.Opening);
        try {
            _webSocket = new WebSocket(WebSocketUrl);
        } catch (Exception e) {
            Debug.Log($"Failed to open webSocketUrl='{WebSocketUrl}' err='{e.Message}'");
            UpdateState(SignalState.Failed);
            if (_webSocket != null) {
                _webSocket.Close();
                _webSocket = null;
            }
            return false;
        }

        _webSocket.OnOpen += OnWebSocketOpen;
        _webSocket.OnMessage += OnWebSocketMessage;
        _webSocket.OnError += OnWebSocketError;
        _webSocket.OnClose += OnWebSocketClose;

        // We Connect _webSocket in a Task (which runs in a thread pool)
        // and do not wait for it to complete here.
        // TODO?: remember the Task so it can be manually interrupted if necessary?
        Task.Run(() => { _webSocket.Connect(); });
        return true;
    }

    void OnWebSocketOpen() {
        Debug.Log("RaviSignaler.OnWebSocketOpen");
        if (State == SignalState.Opening) {
            SanityCheckSignalId();
            UpdateState(SignalState.Connecting);
            string request = "{\"request\":\"" + LocalPeerId + "\"}";
            SendSignal(request);
        } else {
            Debug.Log($"webSocket OnOpen unexpected state={State}");
        }
    }

    void SanityCheckSignalId() {
        Debug.Log("RaviSignaler.SanityCheckSignalId");
        if (string.IsNullOrEmpty(LocalPeerId)) {
            LocalPeerId = Guid.NewGuid().ToString();
            Debug.Log($"RaviSignaler new LocalPeerId='{LocalPeerId}'");
        } else {
            try {
                Guid id = Guid.Parse(LocalPeerId);
            } catch (FormatException) {
                Debug.Log($"RaviSignaler cowardly refusing to use LocalPeerId='{LocalPeerId}'");
                // create a fresh uuid
                LocalPeerId = Guid.NewGuid().ToString();
                Debug.Log($"RaviSignaler new LocalPeerId='{LocalPeerId}'");
            }
        }
    }

    void OnWebSocketMessage(byte[] msg) {
        // Reading a plain text message
        string message = System.Text.Encoding.UTF8.GetString(msg);
        Debug.Log($"RECV signal='{message}'");
        try {
            JSONNode signal = JSON.Parse(message)[LocalPeerId];
            // TODO?: verify signal["uuid"]? It should match what we sent
            if (signal.HasKey("sdp")) {
                if (State == SignalState.Connecting) {
                    UpdateState(SignalState.Signaling);
                }
                HandleSdpSignal(signal);
            } else if (signal.HasKey("ice")) {
                if (State == SignalState.Connecting) {
                    UpdateState(SignalState.Signaling);
                }
                HandleIceSignal(signal["ice"]);
            } else {
                Debug.Log("Unhandled msg");
            }
        } catch (Exception e) {
            Debug.Log("Failed to parse websocket message err='" + e.Message + "'");
        }
    }

    void OnWebSocketError(string msg) {
        Debug.Log($"RaviSignaler.OnWebSocketError err='{msg}'");
    }

    void OnWebSocketClose(NativeWebSocket.WebSocketCloseCode closeCode) {
        Debug.Log($"RaviSignaler.OnWebSocketClose code={closeCode}");
        UpdateState(SignalState.Closed);
    }

    void HandleSdpSignal(JSONNode signal) {
        RTCSessionDescription desc = new RTCSessionDescription();
        try {
            string type = signal["type"];
            string sdp = signal["sdp"];
            if (type == "offer") {
                desc.type = RTCSdpType.Offer;
                desc.sdp = sdp;
                // HandleSdpOffer needs to run as a coroutine
                // so we enqueue it for dispatch on the main thread
                RaviMainThreadDispatcher.Instance().Enqueue(HandleSdpOffer(desc));
            } else {
                // TODO?: handle sdp "answer"?
                Debug.Log($"Unexpected sdp type='{type}'");
            }
        } catch (Exception e) {
            Debug.Log($"Failed to handle sdp signal='{signal.ToString()}' err='{e.Message}'");
        }
        Debug.Log("RaviSignaler.HandleSdpSignal");
    }

    IEnumerator HandleSdpOffer(RTCSessionDescription desc) {
        RTCSetSessionDescriptionAsyncOperation op0;
        try {
            op0 = PeerConnection.SetRemoteDescription(ref desc);
        } catch (Exception e) {
            Debug.Log($"fail SetLocalDescription err={e.Message}");
            UpdateState(SignalState.Failed);
            yield break;
        }
        yield return op0;

        if (op0.IsError) {
            Debug.Log($"fail SetLocalDescription");
            UpdateState(SignalState.Failed);
            yield break;
        }
        Debug.Log($"success SetRemoteDescription type={desc.type} sdp='{desc.sdp}'");

        _answerOptions.iceRestart = false;
        RTCSessionDescriptionAsyncOperation op1 = PeerConnection.CreateAnswer(ref _answerOptions);
        yield return op1;

        if (op1.IsError || string.IsNullOrEmpty(op1.Desc.sdp)) {
            Debug.Log("fail CreateAnswer");
            UpdateState(SignalState.Failed);
            yield break;
        }
        RTCSessionDescription newDesc = op1.Desc;
        Debug.Log($"success CreateAnswer type={newDesc.type} sdp='{newDesc.sdp}'");
        Debug.Log("answer.type=" + newDesc.type + " .sdp='" + newDesc.sdp + "'");

        RTCSetSessionDescriptionAsyncOperation op2;
        try {
            op2 = PeerConnection.SetLocalDescription(ref newDesc);
        } catch (Exception ee) {
            Debug.Log($"fail SetRemoteDescription err='{ee.Message}'");
            UpdateState(SignalState.Failed);
            yield break;
        }
        yield return op2;

        if (op2.IsError) {
            Debug.Log($"fail SetRemoteDescription'");
            UpdateState(SignalState.Failed);
            yield break;
        }

        // TODO: port this from JS impl:
        // Force stereo on the downstream stream by munging the SDP
        //answer.sdp = that._forceStereoDown(answer.sdp);
        //RaviUtils.log("Answer:", "RaviWebRTCImplementation");
        //RaviUtils.log(answer, "RaviWebRTCImplementation");
        // set local description
        //return rtcConnection.setLocalDescription(answer);
        Debug.Log("success SetLocalDescription");
        if (_webSocket != null && _webSocket.State == WebSocketState.Open) {
            JSONNode sdp = new JSONObject();
            sdp["type"] = "answer";
            sdp["sdp"] = PeerConnection.LocalDescription.sdp;
            JSONNode obj = new JSONObject();
            obj["sdp"] = sdp;
            obj["uuid"] = LocalPeerId;
            SendSignal(obj.ToString());
        } else {
            UpdateState(SignalState.Failed);
        }
        Debug.Log("RaviSignaler.HandleSdpOfferAsync");
    }

    void HandleIceSignal(JSONNode signal) {

        RTCIceCandidateInit info = new RTCIceCandidateInit();
        try {
            // parse the ice info
            info.candidate = signal["candidate"];
            if (signal.HasKey("sdpMid")) {
                info.sdpMid = signal["sdpMid"];
            }
            if (signal.HasKey("sdpMLineIndex")) {
                info.sdpMLineIndex = signal["sdpMLineIndex"].AsInt;
            }

            // add candidate
            RTCIceCandidate candidate = new RTCIceCandidate(info);
            if (candidate != null) {
                bool success = PeerConnection.AddIceCandidate(candidate);
                if (!success) {
                    Debug.Log("Failed to AddIceCandidate '" + signal.ToString() + "'");
                }
            }
        } catch (Exception e) {
            Debug.Log("Failed to handle ice signal='" + signal.ToString() + "' err=" + e.Message);
        }
        Debug.Log("RaviSignaler.HandleIceSignal");
    }

    /*
    // HACK: periodic dump of webrtc info
    private IEnumerator DumpSdp() {
        Debug.Log("start DumpSdp()");
        while (true) {
            yield return new WaitForSeconds(1);
            var desc = PeerConnection.LocalDescription;
            Debug.Log($"peerConnection sdp: {desc.sdp}");
        }
    }
    */

    void HandleIceConnectionChange(RTCIceConnectionState state) {
        // mostly for debug so we can see how states evolve
        switch (state) {
            case RTCIceConnectionState.New:
                Debug.Log("IceConnectionState: New");
                break;
            case RTCIceConnectionState.Checking:
                Debug.Log("IceConnectionState: Checking");
                break;
            case RTCIceConnectionState.Closed:
                Debug.Log("IceConnectionState: Closed");
                break;
            case RTCIceConnectionState.Completed:
                Debug.Log("IceConnectionState: Completed");
                break;
            case RTCIceConnectionState.Connected:
                Debug.Log("IceConnectionState: Connected");
                break;
            case RTCIceConnectionState.Disconnected:
                Debug.Log("IceConnectionState: Disconnected");
                break;
            case RTCIceConnectionState.Failed:
                Debug.Log("IceConnectionState: Failed");
                break;
            case RTCIceConnectionState.Max:
                Debug.Log("IceConnectionState: Max");
                break;
            default:
                break;
        }
    }

    void HandleIceGatheringStateChange(RTCIceGatheringState state) {
        Debug.Log($"OnIceGatheringStateChange state={state}");
        if (state == RTCIceGatheringState.Complete) {
            // Send empty ice.candidate to signal "end of ice candidates"
            JSONNode ice = new JSONObject();
            ice["candidate"] = "";
            ice["sdpMid"] = "data";
            ice["sdpMLineIndex"] = 0;
            JSONNode obj = new JSONObject();
            obj["ice"] = ice;
            obj["uuid"] = LocalPeerId;
            SendSignal(obj.ToString());
        }
    }

    void HandleIceCandidate(RTCIceCandidate candidate) {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open) {
            // the ice candidate has been discovered for local peer
            // and we want to communicate it to the remote peer
            JSONNode ice = new JSONObject();
            ice["candidate"] = candidate.Candidate;
            ice["sdpMid"] = candidate.SdpMid;
            ice["sdpMLineIndex"] = candidate.SdpMLineIndex;
            ice["usernameFragment"] = candidate.UserNameFragment;
            JSONNode obj = new JSONObject();
            obj["ice"] = ice;
            obj["uuid"] = LocalPeerId;
            SendSignal(obj.ToString());
        }
    }

    void OnNegotiationNeeded() {
        Debug.Log("OnNegotiationNeeded");
        //var signalingState = PeerConnection.SignalingState;
        //Debug.Log($"OnNegotiationNeeded() state={signalingState}");
        /* TODO: port this from JS impl:
        RaviUtils.log("need renegotiation please", "RaviWebRTCImplementation");
        const msg = {
            renegotiate: "please",
            uuid: this._raviSession.getUUID()
        };
        const desc = JSON.stringify(msg);

        // negotiation needed but only if we are not already currently negotiating
        if (this._signalingConnection && this.PeerConnection.signalingState === "stable") {
            this._signalingConnection.send(desc);
        }
        */
    }
}

} // namespace Ravi

