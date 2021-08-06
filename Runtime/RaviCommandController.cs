// Ravi.cs
//
// Unity plugin for communicating with Ravi server

using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

namespace Ravi {

public class RaviCommandController {
    // there are two handler types: binary and text
    public delegate void BinaryMessageDelegate(byte[] msg);
    public delegate void TextMessageDelegate(string msg);

    private Dictionary<string, TextMessageDelegate> _commandHandlers;
    public BinaryMessageDelegate BinaryCommandHandler;
    public BinaryMessageDelegate BinaryInputHandler;

    public delegate void CommandControllerChangedDelegate();
    public event CommandControllerChangedDelegate OnOpen;
    public event CommandControllerChangedDelegate OnClose;

    private RTCDataChannel _commandChannel;
    private RTCDataChannel _inputChannel;

    private bool _wasOpen = false;

    public RaviCommandController() {
        _commandHandlers = new Dictionary<string, TextMessageDelegate>();
    }

    public bool AddCommandHandler(string key, TextMessageDelegate handler) {
        Log.UncommonEvent(this, "AddCommandHandler key='{0}'", key);
        if (String.IsNullOrEmpty(key)) {
            Log.Warning(this, "AddCommandHandler cowardly refuses to add handler for empty key");
            return false;
        }
        if (handler == null) {
            Log.Warning(this, "AddCommandHandler cowardly refuses to add null handler for key='{0}'", key);
            return false;
        }
        if (_commandHandlers.ContainsKey(key)) {
            _commandHandlers[key] = handler;
        } else {
            _commandHandlers.Add(key, handler);
        }
        return true;
    }

    public bool RemoveCommandHandler(string key) {
        if (_commandHandlers.ContainsKey(key)) {
            _commandHandlers.Remove(key);
            return true;
        }
        Log.Warning(this, "RemoveCommandHandler could not find key='{0}'", key);
        return false;
    }

    public bool IsOpen() {
        // The CommandChannel is considered "open" when both DataChannels have arrived and they are open.
        return _commandChannel != null && _commandChannel.ReadyState == RTCDataChannelState.Open
            && _inputChannel != null && _inputChannel.ReadyState == RTCDataChannelState.Open;
    }

    public void OnDataChannel(RTCDataChannel c) {
        Log.UncommonEvent(this, "OnDataChannel label='{0}' ordered={1} protocol='{2}'", c.Label, c.Ordered, c.Protocol);
        if (c.Label == "ravi.command") {
            _commandChannel = c;
            _commandChannel.OnOpen = OnCommandChannelOpen;
            _commandChannel.OnMessage = OnCommandChannelMessage;
            _commandChannel.OnClose = OnCommandChannelClose;
        } else if (c.Label == "ravi.input") {
            _inputChannel = c;
            _inputChannel.OnOpen = OnInputChannelOpen;
            _inputChannel.OnMessage = OnInputChannelMessage;
            _inputChannel.OnClose = OnInputChannelClose;
        } else {
            Log.Warning(this, $"OnDataChannel Ignoring unexpected DataChannel label='{0}'", c.Label);
        }
        bool isOpen = IsOpen();
        if (isOpen && !_wasOpen) {
            _wasOpen = isOpen;
            OnOpen?.Invoke();
        }
    }

    void OnCommandChannelOpen() {
        // we don't normally reach this context because the channel
        // (initiated by remote peer) is open before we know about it
        Log.UncommonEvent(this, "OnCommandChannelOpen");
        bool isOpen = IsOpen();
        if (isOpen && !_wasOpen) {
            _wasOpen = isOpen;
            OnOpen?.Invoke();
        }
    }

    void OnCommandChannelMessage(byte[] msg) {
        string textMsg = System.Text.Encoding.UTF8.GetString(msg);
        try {
            JSONNode obj = JSON.Parse(textMsg);
            string key = obj["c"];
            Log.CommonEvent(this, "OnCommandChannelMessage msg='{0}'", textMsg);
            if (_commandHandlers.ContainsKey(key)) {
                _commandHandlers[key](obj["p"]);
            } else {
                Log.Warning(this, "OnCommandChannelMessage no handler for command='{0}'", textMsg);
            }
        } catch (Exception) {
            // not an error: this is expected logic flow
            // msg is not a JSON string
            if (BinaryCommandHandler != null) {
                try {
                    BinaryCommandHandler(msg);
                } catch (Exception e) {
                    Log.Error(this, "OnCommandChannelMessage failed err='{0}'", e.Message);
                }
            }
        }
    }

    void OnCommandChannelClose() {
        Log.UncommonEvent(this, "OnCommandChannelClose");
        if (_wasOpen) {
            _wasOpen = false;
            OnClose?.Invoke();
        }
    }

    void OnInputChannelOpen() {
        // we don't normally reach this context because the channel
        // (initiated by remote peer) is open before we know about it
        Log.UncommonEvent(this, "OnInputChannelOpen");
        bool isOpen = IsOpen();
        if (isOpen && !_wasOpen) {
            _wasOpen = isOpen;
            OnOpen?.Invoke();
        }
    }

    void OnInputChannelMessage(byte[] msg) {
        Log.CommonEvent(this, "OnInputChannelMessage");
        // We don't expect any messages from the server on _inputChannel
        // but if we did, then this is where we'd handle them.  In an effort to
        // future-proof we offer this hook: try a custom input message handler.
        if (BinaryInputHandler != null) {
            try {
                BinaryInputHandler(msg);
            } catch (Exception e) {
                Log.Error(this, "HandleInputMessage failed err='{0}'", e.Message);
            }
        }
    }

    void OnInputChannelClose() {
        Log.UncommonEvent(this, "OnInputChannelClose");
        if (_wasOpen) {
            _wasOpen = false;
            OnClose?.Invoke();
        }
    }

    public bool SendCommand(string command, JSONNode payload) {
        Log.Debug(this, "SendCommand command='{0}' payload='{1}'", command, payload);
        try {
            JSONNode obj = new JSONObject();
            obj["c"] = command;
            obj["p"] = payload;
            // The HiFi spatial audio API expects "commands" to be sent as text.
            // Note: According to the documentation C# stores strings in UTF16 encoding,
            // however from the examples it appears the WebRTC sends and receives
            // DataChannel text with UTF8 encoding.  This only matters when converting
            // to and from binary, which we aren't doing here.
            _commandChannel.Send(obj.ToString());
        } catch (Exception e) {
            Log.Error(this, "SendCommand failed err='{0}'", e.Message);
            return false;
        }
        return true;
    }

    public bool SendInput(string msg) {
        Log.Debug(this, "SendInput msg='{0}' msg.Length={1}", msg, msg.Length);
        try {
            _inputChannel.Send(System.Text.Encoding.UTF8.GetBytes(msg));
        } catch (Exception e) {
            Log.Error(this, "SendInput failed err='{0}'", e.Message);
            return false;
        }
        return true;
    }
}

} // namespace
