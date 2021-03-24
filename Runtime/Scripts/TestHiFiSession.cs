using System;
using System.Collections;
using System.Collections.Generic;
//using System.Diagnostics;
using UnityEngine;
using Microsoft.MixedReality.WebRTC.Unity;

public class TestHiFiSession : MonoBehaviour {

    // non-WebRTC objects
    //public AudioListener _audioListener;
    public AudioSource _audioSource;
    public HiFi.HiFiSession _hifiSession;

    Vector3 _position;
    const float OSCILLATION_PERIOD = 20.0f;
    float _omega = 2.0f * (float)(Math.PI) / OSCILLATION_PERIOD;
    float _phase = 0.0f; // phase offset at t=0
    float _amplitude = 2.0f;
    float _updateExpiry = 0.0f;
    float _updatePeriod = 1.0f / 20.0f;
    System.Diagnostics.Stopwatch _clock;

    void Awake() {
        Debug.Log("TestHiFiSession:Awake()");

        _audioSource = gameObject.AddComponent<AudioSource>() as AudioSource;
        _hifiSession = gameObject.AddComponent<HiFi.HiFiSession>() as HiFi.HiFiSession;
        _hifiSession.ConnectionStateChangedEvent += OnSessionStateChanged;

        //_hifiSession.SignalingServiceUrl = HiFi.Constants.HIFI_API_SIGNALING_URL;
        //_hifiSession.SignalingServiceUrl = "ws://192.168.1.143:8001/";
        //_hifiSession.JWT = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhcHBfaWQiOiIxNTU3Zjg1Ny1kOWQ5LTRhYzctOGFjYy1hM2IwNmY2MDhhNmQiLCJ1c2VyX2lkIjoidW5pdHktdXNlciIsInNwYWNlX2lkIjoiOGFjYWQ5NWUtZmViNi00MDczLWI3Y2YtYmEyZjA1ZjcxZWUyIiwic3RhY2siOiJhdWRpb25ldC1taXhlci1hcGktYWxwaGEtMDQifQ.cLea6T8iKCeOBUQhJAmIPOE4zWdsIodqwiCHw_ewWjY";
        //_hifiSession.SignalingServiceUrl = "wss://api.highfidelity.com:8001/";
        _hifiSession.SignalingServiceUrl = "ws://192.168.1.143:8701/";
        _hifiSession.JWT = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhcHBfaWQiOiI0ZjM0MGZmMS1mOGQ5LTRhOTktOWVkMi05NzdlMmVmOWY2MTgiLCJ1c2VyX2lkIjoibWFpYSIsInNwYWNlX2lkIjoiNTBhNjk3NDQtMzZlZC00YTJmLWJkYjItNmYzYzQ0Y2YxMTg4Iiwic3RhY2siOiJhdWRpb25ldC1taXhlci1hcGktYWxwaGEtMDUifQ.vA1sJZxQ-OTaSNfmURoPckicvPz_N5L2xk0ebxX01Yc";
        //_hifiSession.SignalingServiceUrl = "ws://192.168.1.143:8887/"; // ravi_03_data
        //_hifiSession.SignalingServiceUrl = "ws://192.168.1.143:8889/"; // ravi_04_audio
        _position = new Vector3(0.0f, 0.0f, 0.0f);
        computeNewPosition(0.0f);
        _hifiSession.Position = _position;
        _clock = new System.Diagnostics.Stopwatch();
    }

    void Start() {
        Debug.Log("TestHiFiSession:Start()");
        _hifiSession.Connect();
        _clock.Start();
    }

    void Update() {
        float time = (float)(_clock.Elapsed.TotalSeconds);
        if (time > _updateExpiry) {
            computeNewPosition(time);
            _hifiSession.Position = _position;
            _updateExpiry = time + _updatePeriod;
        }
    }

    void computeNewPosition(float time) {
        float theta = _omega * time + _phase;
        _position.x = _amplitude * (float)(Math.Sin(theta));
        _position.y = 0.0f;
        //_position.z = _amplitude * (float)(Math.Cos(theta));
        _position.z = 0.0f;
    }

    void OnSessionStateChanged(HiFi.HiFiSession.AudionetConnectionState state) {
        Debug.Log($"TestHiFiSession.OnSessionStateChanged state='{state}'");
    }
}
