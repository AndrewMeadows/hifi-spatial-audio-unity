using System;
using System.Collections;
using System.Collections.Generic;
//using System.Diagnostics;
using UnityEngine;

/// <summary>
/// A simple script that connects to HiFi Spatial Audio Service and updates the User's position.
/// </summary>
public class TestHiFiCommunicator : MonoBehaviour {

    public HiFi.HiFiCommunicator _hiFiCommunicator;

    Vector3 _position;
    const float OSCILLATION_PERIOD = 20.0f;
    float _omega = 2.0f * (float)(Math.PI) / OSCILLATION_PERIOD;
    float _offset = 0.0f; // phase offset at t=0
    float _amplitude = 1.0f;
    float _updateExpiry = 0.0f;
    float _updatePeriod = 1.0f / 20.0f;
    System.Diagnostics.Stopwatch _clock;

    void Awake() {
        Debug.Log("TestHiFiCommunicator:Awake()");

        _hiFiCommunicator = gameObject.AddComponent<HiFi.HiFiCommunicator>() as HiFi.HiFiCommunicator;
        _hiFiCommunicator.ConnectionStateChangedEvent += OnSessionStateChanged;

        _hiFiCommunicator.SignalingServiceUrl = "wss://api.highfidelity.com:443/";
        _hiFiCommunicator.JWT = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhcHBfaWQiOiIxNTU3Zjg1Ny1kOWQ5LTRhYzctOGFjYy1hM2IwNmY2MDhhNmQiLCJ1c2VyX2lkIjoiYW5kcmV3Iiwic3BhY2VfaWQiOiI4YWNhZDk1ZS1mZWI2LTQwNzMtYjdjZi1iYTJmMDVmNzFlZTIiLCJzdGFjayI6ImF1ZGlvbmV0LW1peGVyLWFwaS1ob2JieS0wMSJ9.e4LpUo6WLGlKHquuwSjrxscZ31t5wtW-VnoH7IMS71w";

        _position = new Vector3(0.0f, 0.0f, 0.0f);
        ComputeNewPosition(0.0f);
        _hiFiCommunicator.Position = _position;
        _clock = new System.Diagnostics.Stopwatch();
    }

    void Start() {
        Debug.Log("TestHiFiCommunicator:Start()");
        _hiFiCommunicator.ConnectToHiFiAudioAPIServer();
        _clock.Start();

        // uncomment one of the lines below to configure Ravi for CommonEvent, or Debug logging
        // Note: this must be done AFTER _hiFiCommunicator.Awake()
        // because that is where the default log level is set
        //Ravi.Log.GlobalMaxLevel = Log.Level.CommonEvent;
        //Ravi.Log.GlobalMaxLevel = Log.Level.Debug;
    }

    void Update() {
        float time = (float)(_clock.Elapsed.TotalSeconds);
        if (time > _updateExpiry) {
            ComputeNewPosition(time);
            _hiFiCommunicator.Position = _position;
            _updateExpiry = time + _updatePeriod;
        }
    }

    void ComputeNewPosition(float time) {
        float theta = _omega * time + _offset;
        _position.x = _amplitude * (float)(Math.Sin(theta));
        _position.y = 0.0f;
        _position.z = _amplitude * (float)(Math.Cos(theta));
    }

    void OnSessionStateChanged(HiFi.HiFiCommunicator.AudionetConnectionState state) {
        Debug.Log($"TestHiFiCommunicator.OnSessionStateChanged state='{state}'");
    }
}
