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
    public string Name = "commandController";

    public Microsoft.MixedReality.WebRTC.DataChannel DataChannel {
        set  {
            if (_dataChannel != null) {
                _dataChannel.MessageReceived -= HandleMessage;
            }
            _dataChannel = value;
            if (_dataChannel != null) {
                _dataChannel.MessageReceived += HandleMessage;
            }
        }
        get { return _dataChannel; }
    }

    // there are two handler types: binary and text
    public delegate void HandleBinaryMessageDelegate(byte[] msg);
    public delegate void HandleTextMessageDelegate(string msg);

    // RaviCommandController has one root binary handler: HandleMessage
    // (see below) which will try to parse the data as JSON string and
    // then search for a corresponding custom handler.  If that fails
    // it will apply FallbackBinaryHandler (if it has been set non-null).
    public HandleBinaryMessageDelegate FallbackBinaryHandler;

    private Microsoft.MixedReality.WebRTC.DataChannel _dataChannel;

    // A map of custom handlers supplied by external logic
    private Dictionary<string, HandleTextMessageDelegate> _handlers;

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

    public void HandleMessage(byte[] msg) {
        string textMsg = System.Text.Encoding.UTF8.GetString(msg);
        Debug.Log($"{Name}.HandleMessage msg='{textMsg}'");
        try {
            JSONNode obj = JSON.Parse(textMsg);
            string key = obj["c"];
            if (_handlers.ContainsKey(key)) {
                _handlers[key](obj["p"]);
            } else {
                Debug.Log($"RouteMessage failed to find handler for command='{textMsg}'");
            }
        } catch (Exception e) {
            // not an error: this is expected flow
            // msg is not a JSON string
            if (e.Message == "foo") {
                Debug.Log($"{Name} should not reach here");
            }
            if (FallbackBinaryHandler != null) {
                try {
                    FallbackBinaryHandler(msg);
                } catch (Exception ee) {
                    Debug.Log($"{Name}.HandleMessage failed err='{ee.Message}'");
                }
            }
        }
    }
}

} // namespace
