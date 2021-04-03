using System;
using System.Collections;
using System.Collections.Generic;
//using System.Diagnostics;
using UnityEngine;
using Unity.WebRTC;

public class TestRaviSession : MonoBehaviour {

    //string WebSocketUrl = "ws://192.168.1.143:8701/";
    public string WebSocketUrl = "ws://192.168.1.143:8887/";
    public Ravi.RaviSession _session;

    void Awake() {
        Ravi.Log.UncommonEvent(this, "Awake");
        _session = gameObject.AddComponent<Ravi.RaviSession>() as Ravi.RaviSession;
        _session.SessionStateChangedEvent += OnSessionStateChange;
    }

    void Start() {
        Ravi.Log.UncommonEvent(this, "Start");
    }

    void Update() {
        if (_session != null && _session.State == Ravi.RaviSession.SessionState.New) {
            _session.Connect(WebSocketUrl);
        }
    }

    void OnSessionStateChange(Ravi.RaviSession.SessionState state) {
        Ravi.Log.UncommonEvent(this, "OnSessionStateChange state='{0}'", state);
    }
}
