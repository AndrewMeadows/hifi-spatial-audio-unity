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
    //public string Name = "commandControllerName";

    public Microsoft.MixedReality.WebRTC.DataChannel CommandChannel {
        set  {
            if (_commandChannel != null) {
                _commandChannel.StateChanged -= OnCommandChannelStateChanged;
                _commandChannel.MessageReceived -= HandleCommandMessage;
            }
            _commandChannel = value;
            if (_commandChannel != null) {
                _commandChannel.StateChanged += OnCommandChannelStateChanged;
                _commandChannel.MessageReceived += HandleCommandMessage;
            }
        }
        get { return _commandChannel; }
    }

    public Microsoft.MixedReality.WebRTC.DataChannel InputChannel {
        set  {
            if (_inputChannel != null) {
                _inputChannel.StateChanged -= OnInputChannelStateChanged;
                _inputChannel.MessageReceived -= HandleInputMessage;
            }
            _inputChannel = value;
            if (_inputChannel != null) {
                _inputChannel.StateChanged += OnInputChannelStateChanged;
                _inputChannel.MessageReceived += HandleInputMessage;
            }
        }
        get { return _inputChannel; }
    }

    // there are two handler types: binary and text
    public delegate void HandleBinaryMessageDelegate(byte[] msg);
    public delegate void HandleTextMessageDelegate(string msg);

    private Dictionary<string, HandleTextMessageDelegate> _commandHandlers;
    public HandleBinaryMessageDelegate BinaryCommandHandler;
    public HandleBinaryMessageDelegate BinaryInputHandler;

    public delegate void DataChannelStateChangedDelegate(Microsoft.MixedReality.WebRTC.DataChannel.ChannelState state);
    public event DataChannelStateChangedDelegate CommandChannelStateChangedEvent;
    public event DataChannelStateChangedDelegate InputChannelStateChangedEvent;

    private Microsoft.MixedReality.WebRTC.DataChannel _commandChannel;
    private Microsoft.MixedReality.WebRTC.DataChannel _inputChannel;

    public RaviCommandController() {
        _commandHandlers = new Dictionary<string, HandleTextMessageDelegate>();
    }

    public bool AddHandler(string key, HandleTextMessageDelegate handler) {
        Debug.Log($"RaviCommandController.AddHandler key='{key}'");
        if (String.IsNullOrEmpty(key)) {
            Debug.Log("RaviCommandController.AddHandler cowardly refuses to add handler for empty key");
            return false;
        }
        if (handler == null) {
            Debug.Log($"RaviCommandController.AddHandler cowardly refuses to add null handler for key='{key}'");
            return false;
        }
        if (_commandHandlers.ContainsKey(key)) {
            _commandHandlers[key] = handler;
        } else {
            _commandHandlers.Add(key, handler);
        }
        return true;
    }

    public bool RemoveHandler(string key) {
        if (_commandHandlers.ContainsKey(key)) {
            _commandHandlers.Remove(key);
            return true;
        }
        Debug.Log($"RaviCommandController.RemoveHandler could not find key='{key}'");
        return false;
    }

    private void OnCommandChannelStateChanged() {
        Debug.Log($"RaviCommandController.OnCommandChannelStateChanged state='{_commandChannel.State}'");
        CommandChannelStateChangedEvent?.Invoke(_commandChannel.State);
    }

    private void OnInputChannelStateChanged() {
        Debug.Log($"RaviCommandController.OnInputChannelStateChanged state='{_inputChannel.State}'");
        InputChannelStateChangedEvent?.Invoke(_inputChannel.State);
    }

    public void HandleCommandMessage(byte[] msg) {
        string textMsg = System.Text.Encoding.UTF8.GetString(msg);
        //Debug.Log($"RaviCommandController.HandleCommandMessage msg.Length='{msg.Length}'");
        try {
            JSONNode obj = JSON.Parse(textMsg);
            string key = obj["c"];
            if (_commandHandlers.ContainsKey(key)) {
                _commandHandlers[key](obj["p"]);
            } else {
                Debug.Log($"RaviCommandController.HandleCommandMessage failed to find handler for command='{textMsg}'");
                Debug.Log($"RaviCommandController.HandleCommandMessage failed json='{obj.ToString()}'");
            }
        } catch (Exception) {
            // not an error: this is expected flow
            // msg is not a JSON string
            if (BinaryCommandHandler != null) {
                try {
                    BinaryCommandHandler(msg);
                } catch (Exception e) {
                    Debug.Log($"RaviCommandController.HandleCommandMessage failed err='{e.Message}'");
                }
            }
        }
    }

    public void HandleInputMessage(byte[] msg) {
        // We don't expect any messages from the server on _inputChannel
        // but if we did, then this is where we'd handle them.  In an effort to
        // future-proof we offer this hook: try a custom input message handler.
        if (BinaryInputHandler != null) {
            try {
                BinaryInputHandler(msg);
            } catch (Exception e) {
                Debug.Log($"RaviCommandController.HandleInputMessage failed err='{e.Message}'");
            }
        }
    }

    public bool SendCommand(string command, JSONNode payload) {
        Debug.Log($"RaviCommandController.SendCommand command='{command}' payload='{payload}'");
        try {
            JSONNode obj = new JSONObject();
            obj["c"] = command;
            obj["p"] = payload;
            _commandChannel.SendMessage(System.Text.Encoding.UTF8.GetBytes(obj.ToString()));
        } catch (Exception e) {
            Debug.Log($"RaviCommandController.SendCommand failed err='{e.Message}'");
            return false;
        }
        return true;
    }

    public bool SendInput(string msg) {
        Debug.Log($"RaviCommandController.SendInput msg='{msg}' msg.Length={msg.Length}");
        try {
            _inputChannel.SendMessage(System.Text.Encoding.UTF8.GetBytes(msg));
        } catch (Exception e) {
            Debug.Log($"RaviCommandController.SendTextMessage failed err='{e.Message}'");
            return false;
        }
        return true;
    }

    /*
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
    */
}

} // namespace
