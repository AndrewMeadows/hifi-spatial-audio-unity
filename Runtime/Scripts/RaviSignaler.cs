// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using NativeWebSocket;
using SimpleJSON;
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Ravi {

/// <summary>
/// Ravi signaler over websocket
/// </summary>
[AddComponentMenu("Ravi Signaler for MixedReality-WebRTC")]
public class RaviSignaler : Signaler {
    public enum Verbosity {
        Silent = 0,
        Errors,
        SingleEvents,
        Messages,
        PerFrameEvents
    }

    /// <summary>
    /// Automatically log all errors to the Unity console.
    /// </summary>
    [Tooltip("LogVerbosity: 0=silent ... 3=flood")]
    public Verbosity LogVerbosity = 0;

    /// <summary>
    /// Connect on start
    /// </summary>
    [Tooltip("Connect on start")]
    public bool ConnectOnStart = false;

    /// <summary>
    /// Unique identifier of the local peer.
    /// </summary>
    [Tooltip("Unique identifier of local peer")]
    public string LocalPeerId;

    const string DEFAULT_WEB_SOCKET_URL = "ws://localhost:8889/";

    /// <summary>
    /// The Ravi websocket to connect to
    /// </summary>
    [Tooltip("The web socket to connect to")]
    public string WebSocketUrl = DEFAULT_WEB_SOCKET_URL;

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
            if (message.Type == SdpMessageType.Offer) {
                MessageType = Type.Offer;
            } else if (message.Type == SdpMessageType.Answer) {
                MessageType = Type.Answer;
            } else {
                MessageType = Type.Unknown;
            }
            Data = message.Content;
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
    /// Log helper
    /// </summary>
    private void LogMessage(Verbosity level, string message) {
        if (LogVerbosity >= level && level > Verbosity.Silent) {
            Debug.Log($"RaviSignaler {message}");
        }
    }

    /// <summary>
    /// Try to open a signal connection at the next convenience.
    /// </summary>
    public void Connect(string webSocketUrl = "") {
        LogMessage(Verbosity.SingleEvents, $"Connect url='{webSocketUrl}' state={_state}");
        _connectLater = true;
        if (_state == SignalState.New
            || _state == SignalState.Closed
            || _state == SignalState.Failed)
        {
            if (!string.IsNullOrEmpty(webSocketUrl)) {
                WebSocketUrl = webSocketUrl;
                SanityCheckWebSocketUrl();
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
    }

    private void SanityCheckWebSocketUrl() {
        // sanity check WebSocketUrl
        string originalUrl = WebSocketUrl;
        if (string.IsNullOrEmpty(WebSocketUrl)) {
            WebSocketUrl = DEFAULT_WEB_SOCKET_URL;
        } else {
            if (!WebSocketUrl.StartsWith("ws://")) {
                WebSocketUrl = "ws://" + WebSocketUrl;
            }
            if (!WebSocketUrl.EndsWith("/")) {
                WebSocketUrl += "/";
            }
        }
        if (originalUrl != WebSocketUrl) {
            LogMessage(Verbosity.SingleEvents, $"SanityCheckWebSocketUrl '{originalUrl}' --> '{WebSocketUrl}'");
        }
    }

    /// <summary>
    /// Unity Engine Start() hook
    /// </summary>
    /// <remarks>
    /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html
    /// </remarks>
    private void Start() {
        SanityCheckWebSocketUrl();

        // sanity check LocalPeerId (should be a Uuid)
        if (string.IsNullOrEmpty(LocalPeerId)) {
            // create a fresh uuid
            Guid id = Guid.NewGuid();
            LocalPeerId = id.ToString();
        } else {
            try {
                Guid id = Guid.Parse(LocalPeerId);
            } catch (FormatException e) {
                throw new FormatException($"bad LocalPeerId='{LocalPeerId}' err='{e.Message}'");
            }
        }

        if (ConnectOnStart) {
            StartConnection();
        }
    }

    private void UpdateSignalState(SignalState newState) {
        if (_state != newState) {
            LogMessage(Verbosity.SingleEvents, $"RaviSignalState: {_state}-->{newState}");
            _state = newState;
            SignalStateChangedEvent?.Invoke(_state);
        }
    }

    /// <summary>
    /// Begin process of opening signal channel over websocket.
    /// </summary>
    private void StartConnection() {
        _connectLater = false;
        if (_state == SignalState.New || _state == SignalState.Closed) {
            LogMessage(Verbosity.SingleEvents, "StartConnection");
            UpdateSignalState(SignalState.Connecting);
            StartCoroutine(OpenWebSocket());
        }
    }

    private IEnumerator OpenWebSocket() {
        LogMessage(Verbosity.SingleEvents, $"OpenWebSocket url='{WebSocketUrl}'");
        if (_webSocket != null && _webSocket.State != WebSocketState.Closed) {
            LogMessage(Verbosity.Errors, $"Failed to open existing websocket state={_webSocket.State}");
            yield break;
        }
        try {
            _webSocket = new WebSocket(WebSocketUrl);
        } catch (Exception e) {
            LogMessage(Verbosity.Errors, $"Failed to open WebSocketUrl='{WebSocketUrl}' err='{e.Message}'");
            if (_webSocket != null) {
                _webSocket.Close();
                _webSocket = null;
            }
            UpdateSignalState(SignalState.Failed);
            yield break;
        }

        _webSocket.OnOpen += () => {
            LogMessage(Verbosity.SingleEvents, "webSocket OnOpen");
            UpdateSignalState(SignalState.Open);
            string request = "{\"request\":\"" + LocalPeerId + "\"}";
            LogMessage(Verbosity.Messages, $"SEND login='{request}'");
            _webSocket.SendText(request);
        };

        _webSocket.OnError += (string errorMessage) => {
            LogMessage(Verbosity.Errors, $"webSocket OnError() err='{errorMessage}'");
        };

        _webSocket.OnClose += (closeCode) => {
            LogMessage(Verbosity.SingleEvents, $"webSocket OnClosed! closeCode={closeCode}");
            UpdateSignalState(SignalState.Closed);
        };

        _webSocket.OnMessage += HandleMessage;

        yield return true;

        // yield each frame until _nativePeer is non-null
        while (_nativePeer == null) {
            yield return true;
        }
        LogMessage(Verbosity.SingleEvents, $"nativePeer initialized");

        // We Connect _webSocket in a Task (which runs in a thread pool)
        // and do not wait for it to complete here.
        // TODO?: remember the Task so it can be manually interrupted if necessary?
        // (e.g. on early manual interrupt)
        Task.Run(() => { _webSocket.Connect(); });
    }

    protected override void OnEnable() {
        LogMessage(Verbosity.SingleEvents, "OnEnable");
        base.OnEnable();
    }

    protected override void OnDisable() {
        LogMessage(Verbosity.SingleEvents, "OnDisable");
        base.OnDisable();
    }

    protected override void OnSdpOfferReadyToSend(SdpMessage offer) {
        LogMessage(Verbosity.SingleEvents, "OnSdpOfferReadyToSend");
        base.OnSdpOfferReadyToSend(offer);
    }

    protected override void OnSdpAnswerReadyToSend(SdpMessage answer) {
        LogMessage(Verbosity.SingleEvents, "OnSdpAnswerReadyToSend");
        base.OnSdpAnswerReadyToSend(answer);
    }

    private Task SendMessageImplAsync(RaviSignalMessage message) {
        string text = message.ToRaviSignalText(LocalPeerId);
        LogMessage(Verbosity.Messages, $"SEND signal='{text}'");
        return _webSocket.SendText(text);
    }

    void HandleMessage(byte[] msg) {
        string signalText = System.Text.Encoding.UTF8.GetString(msg);
        LogMessage(Verbosity.Messages, $"RECV signal='{signalText}'");
        try {
            JSONNode obj = JSON.Parse(signalText);
            JSONNode signal = obj[LocalPeerId];
            if (signal.HasKey("sdp")) {
                HandleSdpSignal(signal);
            } else if (signal.HasKey("ice")) {
                HandleIceSignal(signal["ice"]);
            } else {
                LogMessage(Verbosity.Errors, $"Unhandled signal='{signalText}'");
            }
        } catch (Exception e) {
            LogMessage(Verbosity.Errors, "Failed to parse websocket message err='" + e.Message + "'");
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
            LogMessage(Verbosity.Errors, $"Failed to HandleSdpSignal msg='{signalObj.ToString()}' err='{e.Message}'");
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
            LogMessage(Verbosity.Errors, $"Failed to HandleIceSignal msg='{signal.ToString()}' err='{e.Message}'");
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
            StartConnection();
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
            LogMessage(Verbosity.SingleEvents, "DisconnectWebsocket");
            if (_state != SignalState.Closed && _state != SignalState.Failed) {
                UpdateSignalState(SignalState.Closing);
            }
            Task.Run(() => { _webSocket.Close(); });
        }
    }
}

} // namespace Ravi
