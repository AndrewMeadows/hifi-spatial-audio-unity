using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.WebRTC.Unity;

public class TestRaviSession : MonoBehaviour {

    // non-WebRTC objects
    //public AudioListener _audioListener;
    public AudioSource _audioSource;
    public Ravi.RaviSession _raviSession;

    void Awake() {
        Debug.Log("TestRaviSession:Awake()");

        _audioSource = gameObject.AddComponent<AudioSource>() as AudioSource;
        _raviSession = gameObject.AddComponent<Ravi.RaviSession>() as Ravi.RaviSession;
        _raviSession.SignalUrl = "ws://192.168.1.143:8887/"; // ravi_03_data
        //_raviSession.SignalUrl = "ws://192.168.1.143:8889/"; // ravi_04_audio
    }

    void Start() {
        Debug.Log("TestRaviSession:Start()");
        _raviSession.Open();
    }

    void Update() {
    }

    void OnSessionStateChange(Ravi.RaviSession.SessionState state) {
        Debug.Log($"OnSessionStateChange state='{state}'");
    }
}
