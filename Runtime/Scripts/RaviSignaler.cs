// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using NativeWebSocket;
using SimpleJSON;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Ravi {

/// <summary>
/// Ravi signaler over websocket
/// </summary>
[AddComponentMenu("Ravi Signaler for MixedReality-WebRTC")]
public class RaviSignaler : Signaler {
    /// <summary>
    /// Unique identifier of the local peer.
    /// </summary>
    [Tooltip("Unique identifier of local peer")]
    public string LocalPeerId;

    /// <summary>
    /// The Ravi websocket to connect to
    /// For example: wss://api.highfidelity.com:8001/
    /// </summary>
    [Tooltip("The web socket to connect to")]
    public string WebSocketUrl = HiFi.Constants.DEFAULT_LOCAL_TEST_SIGNALING_URL;

    /// <summary>
    /// The Json Web Token used to identify user
    /// For more info see https://www.highfidelity.com/api/guides/misc/getAJWT
    /// </summary>
    public string JWT;

    /// <summary>
    /// Signal message abstraction.
    /// </summary>
    [Serializable]
    private class RaviSignalMessage {
        /// <summary>
        /// Separator for ICE message components.
        /// </summary>
        public const string IceDelimiter = "|";

        /// <summary>
        /// Possible message types
        /// </summary>
        public enum Type {
            /// <summary>
            /// An unrecognized message.
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// A SDP offer message.
            /// </summary>
            Offer,

            /// <summary>
            /// A SDP answer message.
            /// </summary>
            Answer,

            /// <summary>
            /// A trickle-ice or ice message.
            /// </summary>
            Ice
        }

        /// <summary>
        /// Convert a message type from <see xref="string"/> to <see cref="Type"/>.
        /// </summary>
        /// <param name="stringType">The message type as <see xref="string"/>.</param>
        /// <returns>The message type as a <see cref="Type"/> object.</returns>
        public static Type MessageTypeFromString(string stringType) {
            if (string.Equals(stringType, "offer", StringComparison.OrdinalIgnoreCase)) {
                return Type.Offer;
            } else if (string.Equals(stringType, "answer", StringComparison.OrdinalIgnoreCase)) {
                return Type.Answer;
            }
            throw new InvalidOperationException($"Unkown signaler message type '{stringType}'");
        }

        public IceCandidate ToIceCandidate() {
            if (MessageType != Type.Ice) {
                throw new InvalidOperationException("Expected an IceCandidate message.");
            }
            var parts = Data.Split(new string[] { IceDelimiter }, StringSplitOptions.RemoveEmptyEntries);
            return new IceCandidate {
                SdpMid = parts[2],
                SdpMlineIndex = int.Parse(parts[1]),
                Content = parts[0]
            };
        }

        public RaviSignalMessage(SdpMessage message) {
            Data = message.Content;
            if (message.Type == SdpMessageType.Offer) {
                MessageType = Type.Offer;
            } else if (message.Type == SdpMessageType.Answer) {
                MessageType = Type.Answer;
            } else {
                MessageType = Type.Unknown;
            }
            IceDataSeparator = string.Empty;
        }

        public RaviSignalMessage(IceCandidate candidate) {
            MessageType = Type.Ice;
            Data = string.Join(IceDelimiter, candidate.Content, candidate.SdpMlineIndex.ToString(), candidate.SdpMid);
            IceDataSeparator = IceDelimiter;
        }

        public string ToRaviSignalText(string localPeerId) {
            if (MessageType == Type.Unknown) {
                throw new InvalidOperationException("RaviSignalMessage.Type is unknown");
            }

            JSONNode obj = new JSONObject();
            obj["uuid"] = localPeerId;
            if (MessageType == Type.Offer) {
                JSONNode sdp = new JSONObject();
                sdp["type"] = "offer";
                sdp["sdp"] = Data;
                obj["sdp"] = sdp;
            } else if (MessageType == Type.Answer) {
                JSONNode sdp = new JSONObject();
                sdp["type"] = "answer";
                sdp["sdp"] = Data;
                obj["sdp"] = sdp;
            } else if (MessageType == Type.Ice) {
                var parts = Data.Split(new string[] { IceDelimiter }, StringSplitOptions.RemoveEmptyEntries);
                JSONNode ice = new JSONObject();
                ice["candidate"] = parts[0];
                ice["sdpMid"] = parts[2];
                ice["sdpMLineIndex"] = int.Parse(parts[1]);
                obj["ice"] = ice;
            }
            return obj.ToString();
        }

        /// <summary>
        /// The message type.
        /// </summary>
        public Type MessageType = Type.Unknown;

        /// <summary>
        /// The primary message contents.
        /// </summary>
        public string Data;

        /// <summary>
        /// The data separator needed for proper ICE serialization.
        /// </summary>
        public string IceDataSeparator;
    }

    private WebSocket _webSocket;

    public enum SignalState {
        New = 0,
        Connecting,
        Open,
        Closing,
        Closed,
        Failed
    }

    private SignalState _state = SignalState.New;

    public SignalState State {
        get { return _state; }
    }

    private bool _connectLater = false;

    public delegate void SignalStateChangeDelegate(SignalState state);

    /// <summary>
    /// SignalStateChangedEvent will be invoked when SignalState changes.
    /// </summary>
    public event SignalStateChangeDelegate SignalStateChangedEvent;

    #region ISignaler interface

    /// <inheritdoc/>
    public override Task SendMessageAsync(SdpMessage message) {
        return SendMessageImplAsync(new RaviSignalMessage(message));
    }

    /// <inheritdoc/>
    public override Task SendMessageAsync(IceCandidate candidate) {
        return SendMessageImplAsync(new RaviSignalMessage(candidate));
    }

    #endregion

    /// <summary>
    /// Try to open a signal connection at the next convenience.
    /// </summary>
    public void Connect(string webSocketUrl = "") {
        HiFi.LogUtil.LogUncommonEvent(this, "Connect url='{0}' state={1}", webSocketUrl, _state);
        _connectLater = true;
        if (_state == SignalState.New
            || _state == SignalState.Closed
            || _state == SignalState.Failed)
        {
            if (string.IsNullOrEmpty(webSocketUrl)) {
                WebSocketUrl = HiFi.Constants.DEFAULT_LOCAL_TEST_SIGNALING_URL;
            } else {
                // NOTE: RaviSignaler does not sanity check the webSockerUrl format
                WebSocketUrl = webSocketUrl;
            }
        }
    }

    /// <summary>
    /// Unity Engine Awake() hook.  Looks for PeerConnection component on gameObject if not already set.
    /// </summary>
    /// <remarks>
    /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Awake.html
    /// </remarks>
    void Awake() {
        if (PeerConnection ==  null) {
            // expect a PeerConnection component on gameObject
            PeerConnection = gameObject.GetComponent(typeof (Microsoft.MixedReality.WebRTC.Unity.PeerConnection))
                as Microsoft.MixedReality.WebRTC.Unity.PeerConnection;
        }
        if (string.IsNullOrEmpty(LocalPeerId)) {
            LocalPeerId = Guid.NewGuid().ToString();
        }
    }

    private void UpdateSignalState(SignalState newState) {
        if (_state != newState) {
            HiFi.LogUtil.LogUncommonEvent(this, "UpdateSignalState {0}-->{1}", _state, newState);
            _state = newState;
            SignalStateChangedEvent?.Invoke(_state);
        }
    }

    private IEnumerator OpenWebSocket() {
        HiFi.LogUtil.LogUncommonEvent(this, "OpenWebSocket url='{0}'", WebSocketUrl);
        if (_webSocket != null && _webSocket.State != WebSocketState.Closed) {
            HiFi.LogUtil.LogError(this, "OpenWebSocket failed to open existing websocket with state={1}", _webSocket.State);
            yield break;
        }
        try {
            _webSocket = new WebSocket(WebSocketUrl);
        } catch (Exception e) {
            HiFi.LogUtil.LogError(this, "OpenWebSocket failed to open WebSocketUrl='{0}' err='{1}'", WebSocketUrl, e.Message);
            if (_webSocket != null) {
                _webSocket.Close();
                _webSocket = null;
            }
            UpdateSignalState(SignalState.Failed);
            yield break;
        }

        _webSocket.OnOpen += () => {
            HiFi.LogUtil.LogUncommonEvent(this, "webSocket OnOpen");
            UpdateSignalState(SignalState.Open);
            string request = "{\"request\":\"" + LocalPeerId + "\"}";
            HiFi.LogUtil.LogUncommonEvent(this, "OpenWebSocket SEND login='{0}'", request);
            _webSocket.SendText(request);
        };

        _webSocket.OnError += (string errorMessage) => {
            HiFi.LogUtil.LogError(this, "OpenWebSocket webSocket OnError() err='{0}'", errorMessage);
        };

        _webSocket.OnClose += (closeCode) => {
            HiFi.LogUtil.LogUncommonEvent(this, "OpenWebSocket webSocket OnClosed! closeCode={0}", closeCode);
            UpdateSignalState(SignalState.Closed);
        };

        _webSocket.OnMessage += HandleMessage;

        yield return true;

        // yield each frame until _nativePeer is non-null
        while (_nativePeer == null) {
            yield return true;
        }
        HiFi.LogUtil.LogDebug(this, "OpenWebSocket nativePeer initialized");

        // We Connect _webSocket in a Task (which runs in a thread pool)
        // and do not wait for it to complete here.
        // TODO?: remember the Task so it can be manually interrupted if necessary?
        // (e.g. on early manual interrupt)
        Task.Run(() => { _webSocket.Connect(); });
    }

    protected override void OnEnable() {
        HiFi.LogUtil.LogUncommonEvent(this, "OnEnable");
        base.OnEnable();
    }

    protected override void OnDisable() {
        HiFi.LogUtil.LogUncommonEvent(this, "OnDisable");
        base.OnDisable();
    }

    protected override void OnSdpOfferReadyToSend(SdpMessage offer) {
        HiFi.LogUtil.LogDebug(this, "OnSdpOfferReadyToSend");
        base.OnSdpOfferReadyToSend(offer);
    }

    protected override void OnSdpAnswerReadyToSend(SdpMessage answer) {
        HiFi.LogUtil.LogDebug(this, "OnSdpAnswerReadyToSend");
        base.OnSdpAnswerReadyToSend(answer);
    }

    private Task SendMessageImplAsync(RaviSignalMessage message) {
        string text = message.ToRaviSignalText(LocalPeerId);
        HiFi.LogUtil.LogCommonEvent(this, "SEND signal='{0}'", text);
        return _webSocket.SendText(text);
    }

    void HandleMessage(byte[] msg) {
        string signalText = System.Text.Encoding.UTF8.GetString(msg);
        HiFi.LogUtil.LogCommonEvent(this, "RECV signal='{0}'", signalText);
        try {
            JSONNode obj = JSON.Parse(signalText);
            JSONNode signal = obj[LocalPeerId];
            if (signal.HasKey("sdp")) {
                HandleSdpSignal(signal);
            } else if (signal.HasKey("ice")) {
                HandleIceSignal(signal["ice"]);
            } else {
                HiFi.LogUtil.LogWarning(this, "Unhandled signal='{0}'", signalText);
            }
        } catch (Exception e) {
            HiFi.LogUtil.LogError(this, "HandleMessage failed to parse err='{0}'", e.Message);
        }
    }

    void HandleSdpSignal(JSONNode signalObj) {
        try {
            string content = signalObj["sdp"];
            string type = signalObj["type"];
            if (type == "offer") {
                // Apply the offer coming from the remote peer to the local peer
                var sdpOffer = new SdpMessage { Type = SdpMessageType.Offer, Content = content };
                PeerConnection.HandleConnectionMessageAsync(sdpOffer).ContinueWith(_ =>
                {
                    // If the remote description was successfully applied then immediately send
                    // back an answer to the remote peer to acccept the offer.
                    _nativePeer.CreateAnswer();
                }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);
            } else if (type == "answer") {
                // No need to wait for completion; there is nothing interesting to do after it.
                var sdpAnswer = new SdpMessage { Type = SdpMessageType.Answer, Content = content };
                _ = PeerConnection.HandleConnectionMessageAsync(sdpAnswer);
            }
        } catch (Exception e) {
            HiFi.LogUtil.LogError(this, "HandleSdpSignal failed msg='{0}' err='{1}'", signalObj.ToString(), e.Message);
        }
    }

    void HandleIceSignal(JSONNode signal) {
        try {
            string content = signal["candidate"];
            string sdpMid = "";
            if (signal.HasKey("sdpMid")) {
                sdpMid = signal["sdpMid"];
            }
            int sdpMLineIndex = 0;
            if (signal.HasKey("sdpMLineIndex")) {
                sdpMLineIndex = signal["sdpMLineIndex"].AsInt;
            }

            IceCandidate ice = new IceCandidate {
                SdpMid = sdpMid,
                SdpMlineIndex = sdpMLineIndex,
                Content = content
            };
            _nativePeer.AddIceCandidate(ice);
        } catch (Exception e) {
            HiFi.LogUtil.LogError(this, "HandleIceSignal failed msg='{0}' err='{1}'", signal.ToString(), e.Message);
        }
    }

    protected override void Update() {
        // Update the base
        base.Update();

        // pump the websocket
        if (_webSocket != null) {
            if (_webSocket.State == WebSocketState.Open) {
                #if !UNITY_WEBGL || UNITY_EDITOR
                _webSocket.DispatchMessageQueue();
                #endif
            }
        }
        if (_connectLater) {
            _connectLater = false;
            if (_state == SignalState.New || _state == SignalState.Closed) {
                // The Ravi service expects the Signaler's LocalPeerId to be a UUID string in 4221 format.
                if (string.IsNullOrEmpty(LocalPeerId)) {
                    // create a fresh uuid
                    LocalPeerId = Guid.NewGuid().ToString();
                } else {
                    try {
                        Guid id = Guid.Parse(LocalPeerId);
                    } catch (FormatException) {
                        HiFi.LogUtil.LogError(this, "cowardly refusing to use LocalPeerId='{0}'", LocalPeerId);
                        // create a fresh uuid
                        LocalPeerId = Guid.NewGuid().ToString();
                    }
                }

                HiFi.LogUtil.LogUncommonEvent(this, "StartConnection LocalPeerId={0}", LocalPeerId);
                UpdateSignalState(SignalState.Connecting);
                StartCoroutine(OpenWebSocket());
            }
        }
    }

    /// <summary>
    /// Close websocket signal channel.
    /// </summary>
    public void DisconnectWebsocket() {
        if (_webSocket != null
            && _webSocket.State != WebSocketState.Closing
            && _webSocket.State != WebSocketState.Closed)
        {
            HiFi.LogUtil.LogUncommonEvent(this, "DisconnectWebsocket");
            if (_state != SignalState.Closed && _state != SignalState.Failed) {
                UpdateSignalState(SignalState.Closing);
            }
            Task.Run(() => { _webSocket.Close(); });
        }
    }
}

} // namespace Ravi
