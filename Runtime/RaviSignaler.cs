// RaviSignaler.cs
//
using NativeWebSocket;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

namespace Ravi {

/// <summary>
/// Implementation of Signaler for Ravi Service.
/// </summary>
public class RaviSignaler : MonoBehaviour {
    public enum SignalState {
        New,
        Opening,
        Connecting,
        Signaling,
        Stable,
        Disconnected,
        Failed,
        Closed,
        Unavailable
    }

    public delegate void SignalStateChangeDelegate(SignalState state);

    public string WebSocketUrl;
    public string LocalPeerId; // Uuid string in 4211 format
    public SignalState State { get; internal set; }
    public RTCPeerConnection PeerConnection; // set by external RaviSession
    public event SignalStateChangeDelegate SignalStateChangedEvent; // DANGER: not necessarily fired on main thread!

    WebSocket _webSocket;

    RTCOfferAnswerOptions _answerOptions = new RTCOfferAnswerOptions {
        iceRestart = false,
    };

    public RaviSignaler() {
        State = SignalState.New;
    }

    void Awake() {
    }

    void Start() {
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
            Log.UncommonEvent(this, "UpdateState {0}-->{1}", State, newState);
            State = newState;
            this.SignalStateChangedEvent?.Invoke(newState);
        }
    }

    void OnDestroy() {
    }

    void SendSignal(string text) {
        Log.CommonEvent(this, "SEND signal='{0}'", text);
        _webSocket.SendText(text);
    }

    void LatchOntoPeerConnection() {
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
        // and these are the delegate of interest to this Signaler:
        PeerConnection.OnIceConnectionChange = HandleIceConnectionChange;
        PeerConnection.OnIceGatheringStateChange = HandleIceGatheringStateChange;
        PeerConnection.OnIceCandidate = HandleIceCandidate;
        PeerConnection.OnNegotiationNeeded = OnNegotiationNeeded;
    }

    /// <summary>
    /// Connect to Ravi websocket ip:port
    /// </summary>
    public bool Connect(string url) {
        Log.UncommonEvent(this, "Connect url='{0}'", url);
        if (PeerConnection == null) {
            // Note: PeerConnection must be set by external RaviSession before trying to Connect
            Log.Error(this, "Connect failed for null PeerConnection");
            UpdateState(SignalState.Failed);
            return false;
        }

        if (State == SignalState.Failed || State == SignalState.Unavailable) {
            UpdateState(SignalState.Closed);
        }
        if (State == SignalState.New || State == SignalState.Closed) {
            WebSocketUrl = url;

            bool success = OpenWebSocket();
            if (success) {
                Log.Debug(this, "Connect successful OpenWebSocket");
                LatchOntoPeerConnection();
            } else {
                Log.Error(this, "Connect failed to OpenWebSocket");
                UpdateState(SignalState.Failed);
            }
            return success;
        }
        Log.Error(this, "Connect failed for incorrect initial state");
        return false;
    }

    /// <summary>
    /// Disconnect from Ravi websocket
    /// </summary>
    public void Disconnect() {
        if (_webSocket != null) {
            _webSocket.Close();
        }
    }

    bool OpenWebSocket() {
        Log.Debug(this, "OpenWebSocket");
        UpdateState(SignalState.Opening);
        try {
            _webSocket = new WebSocket(WebSocketUrl);
        } catch (Exception e) {
            Log.Error(this, "failed to open webSocketUrl='{0}' err='{1}'", WebSocketUrl, e.Message);
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
        Log.Debug(this, "OnWebSocketOpen");
        if (State == SignalState.Opening) {
            SanityCheckSignalId();
            UpdateState(SignalState.Connecting);
            string request = "{\"request\":\"" + LocalPeerId + "\"}";
            SendSignal(request);
        } else {
            Log.Warning(this, "webSocket OnOpen unexpected state={0}", State);
        }
    }

    void SanityCheckSignalId() {
        Log.Debug(this, "SanityCheckSignalId");
        if (string.IsNullOrEmpty(LocalPeerId)) {
            LocalPeerId = Guid.NewGuid().ToString();
            Log.UncommonEvent(this, "new LocalPeerId='{0}'", LocalPeerId);
        } else {
            try {
                Guid id = Guid.Parse(LocalPeerId);
            } catch (FormatException) {
                Log.Warning(this, "cowardly refusing to use LocalPeerId='{0}'", LocalPeerId);
                // create a fresh uuid
                LocalPeerId = Guid.NewGuid().ToString();
                Log.UncommonEvent(this, "new LocalPeerId='{0}'", LocalPeerId);
            }
        }
    }

    void OnWebSocketMessage(byte[] msg) {
        // Reading a plain text message
        string message = System.Text.Encoding.UTF8.GetString(msg);
        Log.CommonEvent(this, "RECV signal='{0}'", message);
        try {
            JSONNode signal = JSON.Parse(message)[LocalPeerId];
            // TODO?: verify signal["uuid"]? It should match what we sent
            if (signal.HasKey("error") && signal["error"] == "service-unavailable") {
                UpdateState(SignalState.Unavailable);
            } else if (signal.HasKey("sdp")) {
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
                Log.Debug(this, "Unhandled signal='{0}'", message);
            }
        } catch (Exception e) {
            Log.Warning(this, "failed to parse websocket message err='{0}'", e.Message);
        }
    }

    void OnWebSocketError(string msg) {
        Log.Warning(this, "OnWebSocketError err='{0}'", msg);
    }

    void OnWebSocketClose(NativeWebSocket.WebSocketCloseCode closeCode) {
        Log.UncommonEvent(this, "OnWebSocketClose code={0}", closeCode);
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
                // TODO: make sure we dispatch it on the main thread
                StartCoroutine(HandleSdpOffer(desc));
            } else {
                // TODO?: handle sdp "answer"?
                Log.Warning(this, "Unexpected sdp type='{0}'", type);
            }
        } catch (Exception e) {
            Log.Warning(this, "failed to handle sdp signal='{0}' err='{1}'", signal.ToString(), e.Message);
        }
        Log.Debug(this, "HandleSdpSignal complete");
    }

    IEnumerator HandleSdpOffer(RTCSessionDescription desc) {
        Log.UncommonEvent(this, "HandleSdpOffer sdp={0}", desc.sdp);
        RTCSetSessionDescriptionAsyncOperation op0;
        try {
            op0 = PeerConnection.SetRemoteDescription(ref desc);
        } catch (Exception e) {
            Log.Error(this, "fail SetLocalDescription err='{0}'", e.Message);
            UpdateState(SignalState.Failed);
            yield break;
        }
        yield return op0;

        if (op0.IsError) {
            Log.Error(this, "SetLocalDescription IsError");
            UpdateState(SignalState.Failed);
            yield break;
        }
        Log.Debug(this, "success SetRemoteDescription type={0} sdp='{1}'", desc.type, desc.sdp);

        _answerOptions.iceRestart = false;
        RTCSessionDescriptionAsyncOperation op1 = PeerConnection.CreateAnswer(ref _answerOptions);
        yield return op1;

        if (op1.IsError || string.IsNullOrEmpty(op1.Desc.sdp)) {
            Log.Error(this, "fail CreateAnswer");
            UpdateState(SignalState.Failed);
            yield break;
        }
        RTCSessionDescription newDesc = op1.Desc;
        Log.UncommonEvent(this, "successful CreateAnswer type={0} sdp='{1}'", newDesc.type, newDesc.sdp);

        // HACK: munge the answer sdp to request stereo down
        // TODO: implement this by setting parameters/constraints on PeerConnection
        // or better yet: copy the fmtp_field from the offer and echo it back.
        string fmtp_pattern = "a=fmtp:111.*\r\n";
        Match m = Regex.Match(newDesc.sdp, fmtp_pattern);
        if (m.Success) {
            string fmtp_field = m.Value;
            string stereo_pattern = "stereo=[01]";
            m = Regex.Match(fmtp_field, stereo_pattern);
            if (m.Success) {
                fmtp_field = Regex.Replace(fmtp_field, stereo_pattern, "stereo=1", RegexOptions.None, TimeSpan.FromSeconds(0.5));
            } else {
                fmtp_field = String.Format("{0};{1};{2}\r\n", fmtp_field.Trim(), "sprop-stereo=1", "stereo=1");
            }
            newDesc.sdp = Regex.Replace(newDesc.sdp, fmtp_pattern, fmtp_field, RegexOptions.None, TimeSpan.FromSeconds(0.5));
        }

        // set local description
        RTCSetSessionDescriptionAsyncOperation op2;
        try {
            op2 = PeerConnection.SetLocalDescription(ref newDesc);
        } catch (Exception ee) {
            Log.Error(this, "fail SetRemoteDescription err='{0}'", ee.Message);
            UpdateState(SignalState.Failed);
            yield break;
        }
        yield return op2;

        if (op2.IsError) {
            Log.Error(this, "fail SetRemoteDescription");
            UpdateState(SignalState.Failed);
            yield break;
        }

        Log.Debug(this, "success SetLocalDescription");
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
        Log.Debug(this, "HandleSdpOfferAsync complete");
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
                    Log.Error(this, "failed to AddIceCandidate '{}'", signal.ToString());
                }
            }
        } catch (Exception e) {
            Log.Warning(this, "failed to handle ice signal='{0}' err='{1}'", signal.ToString(), e.Message);
        }
        Log.Debug(this, "HandleIceSignal complete");
    }

    void HandleIceConnectionChange(RTCIceConnectionState state) {
        // mostly for debug so we can see how states evolve
        switch (state) {
            case RTCIceConnectionState.New:
                Log.CommonEvent(this, "IceConnectionState: New");
                break;
            case RTCIceConnectionState.Checking:
                Log.CommonEvent(this, "IceConnectionState: Checking");
                break;
            case RTCIceConnectionState.Closed:
                Log.CommonEvent(this, "IceConnectionState: Closed");
                break;
            case RTCIceConnectionState.Completed:
                Log.CommonEvent(this, "IceConnectionState: Completed");
                break;
            case RTCIceConnectionState.Connected:
                Log.CommonEvent(this, "IceConnectionState: Connected");
                break;
            case RTCIceConnectionState.Disconnected:
                Log.CommonEvent(this, "IceConnectionState: Disconnected");
                break;
            case RTCIceConnectionState.Failed:
                Log.CommonEvent(this, "IceConnectionState: Failed");
                break;
            case RTCIceConnectionState.Max:
                Log.CommonEvent(this, "IceConnectionState: Max");
                break;
            default:
                break;
        }
    }

    void HandleIceGatheringStateChange(RTCIceGatheringState state) {
        Log.UncommonEvent(this, "OnIceGatheringStateChange state={0}", state);
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
        Log.UncommonEvent(this, "OnNegotiationNeeded");
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

