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
    bool _updateLogged = false;

    void Awake() {
        Ravi.Log.UncommonEvent(this, "Awake");

        _session = gameObject.AddComponent<Ravi.RaviSession>() as Ravi.RaviSession;
        _session.SessionStateChangedEvent += OnSessionStateChange;

        //_signaler.WebSocketUrl = "ws://192.168.1.143:8701/";
        //_signaler.JWT = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhcHBfaWQiOiI0ZjM0MGZmMS1mOGQ5LTRhOTktOWVkMi05NzdlMmVmOWY2MTgiLCJ1c2VyX2lkIjoibWFpYSIsInNwYWNlX2lkIjoiNTBhNjk3NDQtMzZlZC00YTJmLWJkYjItNmYzYzQ0Y2YxMTg4Iiwic3RhY2siOiJhdWRpb25ldC1taXhlci1hcGktYWxwaGEtMDUifQ.vA1sJZxQ-OTaSNfmURoPckicvPz_N5L2xk0ebxX01Yc";
    }

    void Start() {
        Ravi.Log.UncommonEvent(this, "Start");
    }

    void Update() {
        if (!_updateLogged) {
            Ravi.Log.UncommonEvent(this, "Update");
            _updateLogged = true;
        } else {
            if (_session != null && _session.State == Ravi.RaviSession.SessionState.New) {
                _session.Connect(WebSocketUrl);
            }
        }
    }

    void OnSessionStateChange(Ravi.RaviSession.SessionState state) {
        Ravi.Log.UncommonEvent(this, "OnSessionStateChange state='{0}'", state);
    }
}
