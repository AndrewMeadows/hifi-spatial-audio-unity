// Ravi.cs
//
// Unity plugin for communicating with Ravi server

using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC.Unity;
using UnityEngine;

namespace Ravi {

public class RaviCommandController {
    public string Name = "commandControllerName";

    public Microsoft.MixedReality.WebRTC.DataChannel DataChannel {
        set  {
            if (_dataChannel != null) {
                _dataChannel.StateChanged -= OnDataChannelStateChanged;
                _dataChannel.MessageReceived -= HandleMessage;
            }
            _dataChannel = value;
            if (_dataChannel != null) {
                _dataChannel.StateChanged += OnDataChannelStateChanged;
                _dataChannel.MessageReceived += HandleMessage;
            }
        }
        get { return _dataChannel; }
    }

    // there are two handler types: binary and text
    public delegate void HandleBinaryMessageDelegate(byte[] msg);
    public delegate void HandleTextMessageDelegate(string msg);

    // RaviCommandController has one root binary handler: HandleMessage
    // (see below) which will try to parse the data as JSON string.  If
    // successful it will try to parse the JSON object as a 'c' command with
    // 'p' payload.  It will look in a map of handlers for matching command
    // and submit the message to the corresponding handler.
    private Dictionary<string, HandleTextMessageDelegate> _handlers;

    // If the JSON parse failed, or the JSON object but did not have expected
    // structure, or the command was not found in the map then it will submit
    // the raw messaage data to BinaryHandler.  Ravi users can set this
    // handler to parse custom messages.
    public HandleBinaryMessageDelegate BinaryHandler;

    public delegate void DataChannelStateChangedDelegate(Microsoft.MixedReality.WebRTC.DataChannel.ChannelState state);
    public event DataChannelStateChangedDelegate DataChannelStateChangedEvent;

    private Microsoft.MixedReality.WebRTC.DataChannel _dataChannel;

    public RaviCommandController() {
        _handlers = new Dictionary<string, HandleTextMessageDelegate>();
    }

    public bool AddHandler(string key, HandleTextMessageDelegate handler) {
        if (String.IsNullOrEmpty(key)) {
            Debug.Log($"{Name}.AddHandler cowardly refuses to add handler for empty key");
            return false;
        }
        if (handler == null) {
            Debug.Log($"{Name}.AddHandler cowardly refuses to add null handler for key='{key}'");
            return false;
        }
        if (_handlers.ContainsKey(key)) {
            _handlers[key] = handler;
        } else {
            _handlers.Add(key, handler);
        }
        return true;
    }

    public bool RemoveHandler(string key) {
        if (_handlers.ContainsKey(key)) {
            _handlers.Remove(key);
            return true;
        }
        Debug.Log($"{Name}.RemoveHandler could not find key='{key}'");
        return false;
    }

    private void OnDataChannelStateChanged() {
        Debug.Log($"{Name}.OnDataChannelStateChanged state='{_dataChannel.State}'");
        DataChannelStateChangedEvent?.Invoke(_dataChannel.State);
    }

    public void HandleMessage(byte[] msg) {
        string textMsg = System.Text.Encoding.UTF8.GetString(msg);
        Debug.Log($"{Name}.HandleMessage msg.Length='{msg.Length}'");
        try {
            JSONNode obj = JSON.Parse(textMsg);
            string key = obj["c"];
            if (_handlers.ContainsKey(key)) {
                _handlers[key](obj["p"]);
            } else {
                Debug.Log($"{Name}.HandleMessage failed to find handler for command='{textMsg}'");
                Debug.Log($"{Name}.HandleMessage failed json='{obj.ToString()}'");
            }
        } catch (Exception e) {
            Debug.Log($"{Name}.HandleMessage could not parse msg as Json string"); // adebug
            // not an error: this is expected flow
            // msg is not a JSON string
            if (e.Message == "foo") {
                Debug.Log($"{Name}.HandleMessage should not reach here");
            }
            if (BinaryHandler != null) {
                try {
                    BinaryHandler(msg);
                } catch (Exception ee) {
                    Debug.Log($"{Name}.HandleMessage failed err='{ee.Message}'");
                }
            }
        }
    }

    public bool SendCommand(string command, JSONNode payload) {
        Debug.Log($"{Name}.SendCommand command='{command}' payload='{payload}'");
        JSONNode obj = new JSONObject();
        obj["c"] = command;
        obj["p"] = payload;
        return SendTextMessage(obj.ToString());
    }

    public bool SendTextMessage(string msg) {
        Debug.Log($"{Name}.SendTextCommand msg='{msg}' msg.Length={msg.Length}");
        try {
            _dataChannel.SendMessage(System.Text.Encoding.UTF8.GetBytes(msg));
        } catch (Exception e) {
            Debug.Log($"{Name}.SendTextMessage failed err='{e.Message}'");
            return false;
        }
        return true;
    }
}

} // namespace
