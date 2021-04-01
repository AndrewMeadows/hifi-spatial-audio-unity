using System;
using System.Collections;
using System.Collections.Generic;
//using System.Diagnostics;
using UnityEngine;
using Unity.WebRTC;

public class TestRaviSignaler : MonoBehaviour {

    //string WebSocketUrl = "ws://192.168.1.143:8701/";
    public string WebSocketUrl = "ws://192.168.1.143:8887/";
    public Ravi.RaviSignaler _signaler;
    bool _updateLogged = false;

    void Awake() {
        Debug.Log("TestRaviSignaler:Awake()");

        _signaler = gameObject.AddComponent<Ravi.RaviSignaler>() as Ravi.RaviSignaler;
        _signaler.SignalStateChanged += OnSignalStateChange;

        //_signaler.WebSocketUrl = "ws://192.168.1.143:8701/";
        //_signaler.JWT = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhcHBfaWQiOiI0ZjM0MGZmMS1mOGQ5LTRhOTktOWVkMi05NzdlMmVmOWY2MTgiLCJ1c2VyX2lkIjoibWFpYSIsInNwYWNlX2lkIjoiNTBhNjk3NDQtMzZlZC00YTJmLWJkYjItNmYzYzQ0Y2YxMTg4Iiwic3RhY2siOiJhdWRpb25ldC1taXhlci1hcGktYWxwaGEtMDUifQ.vA1sJZxQ-OTaSNfmURoPckicvPz_N5L2xk0ebxX01Yc";
    }

    void Start() {
        Debug.Log("TestRaviSignaler:Start");
        //_signaler.Connect(WebSocketUrl);
    }

    void Update() {
        if (!_updateLogged) {
            Debug.Log("TestRaviSignaler.Update");
            _updateLogged = true;
        } else {
            if (_signaler != null && _signaler.State == Ravi.RaviSignaler.SignalState.New) {
                _signaler.Connect(WebSocketUrl);
            }
        }
    }

    void OnSignalStateChange(Ravi.RaviSignaler.SignalState state) {
        Debug.Log($"TestRaviSignaler.OnSignalStateChange state='{state}'");
    }
}
