using System;
using System.Collections;
using System.Collections.Generic;
//using System.Diagnostics;
using UnityEngine;
using Unity.WebRTC;

/// <summary>
/// A simple script that performs the WebRTC signal dance with a Ravi server.
/// </summary>
public class TestRaviSignaler : MonoBehaviour {

    public string WebSocketUrl = "wss://api.highfidelity.com:443/";
    public Ravi.RaviSignaler _signaler;
    bool _loggedOnce = false;

    void Awake() {
        Debug.Log("TestRaviSignaler:Awake()");
        _signaler = gameObject.AddComponent<Ravi.RaviSignaler>() as Ravi.RaviSignaler;
        _signaler.SignalStateChangedEvent += OnSignalStateChange;

        // configure Ravi for debug logging
        Ravi.Log.GlobalMaxLevel = Log.Level.Debug;
    }

    void Start() {
        Debug.Log("TestRaviSignaler:Start");
    }

    void Update() {
        if (!_loggedOnce) {
            Debug.Log("TestRaviSignaler.Update");
            _loggedOnce = true;
        } else {
            if (_signaler != null && _signaler.State == Ravi.RaviSignaler.SignalState.New) {
                _signaler.Connect(WebSocketUrl);
            }
        }
    }

    void OnSignalStateChange(Ravi.RaviSignaler.SignalState state) {
        // run the script and watch these state changes
        Debug.Log($"TestRaviSignaler.OnSignalStateChange state='{state}'");
    }
}
