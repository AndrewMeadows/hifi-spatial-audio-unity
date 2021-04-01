using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
//using System.Diagnostics;
using UnityEngine;
using Unity.WebRTC;

/// <summary>
/// A simple test sript for RaviSession.
/// </summary>
public class TestRavi03Data : MonoBehaviour {

    // the intention is you're running a local compatible ravi server
    public string WebSocketUrl = "ws://192.168.1.143:8887/";
    public Ravi.RaviSession _session;

    void Awake() {
        Ravi.Log.UncommonEvent(this, "Awake");

        _session = gameObject.AddComponent<Ravi.RaviSession>() as Ravi.RaviSession;
        _session.SessionStateChangedEvent += OnSessionStateChange;
        _session.CommandController.AddCommandHandler("onMyCommandMessage", OnMyCommandMessage);
    }

    void Start() {
        Ravi.Log.UncommonEvent(this, "Start");
        Ravi.Log.GlobalMaxLevel = Log.Level.CommonEvent;
    }

    void Update() {
        if (_session != null && _session.State == Ravi.RaviSession.SessionState.New) {
            _session.Connect(WebSocketUrl);
        }
    }

    void OnSessionStateChange(Ravi.RaviSession.SessionState state) {
        Ravi.Log.UncommonEvent(this, "OnSessionStateChange state='{0}'", state);
    }

    void OnMyCommandMessage(string msg) {
        Ravi.Log.CommonEvent(this, "OnMyCommandMessage received msg='{0}'", msg);
        try {
            JSONNode obj = JSON.Parse(msg);
            Ravi.Log.UncommonEvent(this, "OnMyCommandMessage JSON='{0}'", obj.ToString());
            Ravi.Log.UncommonEvent(this, "OnMyCommandMessage value={0:D}", obj["value"]);
            Ravi.Log.UncommonEvent(this, "OnMyCommandMessage data='{0}'", obj["data"].ToString());
            //Ravi.Log.UncommonEvent(this, "OnMyCommandMessage value={0} data='{1}'", obj["value"].AsInt, obj["data"].ToString());
        } catch (Exception e) {
            Ravi.Log.Warning(this, "OnMyCommandMessage parse error for msg='{0}' err='{1}'", msg, e.Message);
        }
    }
}
