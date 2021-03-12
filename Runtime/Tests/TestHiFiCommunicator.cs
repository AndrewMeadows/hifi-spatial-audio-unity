using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.WebRTC.Unity;

public class TestHiFiCommunicator : MonoBehaviour {

    // non-WebRTC objects
    //public AudioListener _audioListener;
    public AudioSource _audioSource;
    public HiFi.HiFiCommunicator _hifiCommunicator;

    void Awake() {
        Debug.Log("TestRaviSession:Awake()");

        _audioSource = gameObject.AddComponent<AudioSource>() as AudioSource;
        _hifiCommunicator = gameObject.AddComponent<HiFi.HiFiCommunicator>() as HiFi.HiFiCommunicator;
        _hifiCommunicator.SignalUrl = "ws://192.168.1.143:8887/"; // ravi_03_data
        //_hifiCommunicator.SignalUrl = "ws://192.168.1.143:8889/"; // ravi_04_audio
    }

    void Start() {
        Debug.Log("TestRaviSession:Start()");
        _hifiCommunicator.ConnectToHiFiAudioAPIServer();
    }

    void Update() {
    }

    void OnCommunicatorStateChange(HiFi.HiFiCommunicator.ConnectionState state) {
        Debug.Log($"OnCommunicatorStateChange state='{state}'");
    }
}
