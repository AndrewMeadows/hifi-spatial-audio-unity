// Tester.cs -- simple test/demo for HiFiCommunicator
//

using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tester : MonoBehaviour {

    public string HiFiUrl;
    public string HiFiJwt;
    public Vector3 Position;
    public Quaternion Orientation;
    public HiFi.HiFiCommunicator _communicator;

    private string _audioDeviceName = "unknown";
    private System.Diagnostics.Stopwatch _clock;
    private float _orbitPeriod = 6.0f;
    private float _orbitRadius = 1.0f;
    private double _updateUserDataPeriod = 0.050;
    private double _updateUserDataExpiry = 0.0;
    private bool _muteTheMic = false;
    private Graph _graph;
    private ulong _lastJitterBufferEmittedCount = 0;
    private double _lastJitterBufferDelay = 0.0f;

    void Awake() {
        Debug.Log("Tester.Awake");

        #if EXPERIMENTAL_DEVELOPMENT
        // debug: list all microphone devices
        int i = 0;
        foreach (var device in Microphone.devices) {
            Debug.Log(string.Format("microphone[{0}]='{1}'", i, device));
            i += 1;
        }
        #endif
        _graph = gameObject.AddComponent<Graph>() as Graph;
        _graph.WindowRect = new Rect(20, 20, 800, 400);
        _graph.AddChannel("concealedSamples", "diff", Color.green);
        _graph.AddChannel("insertedSamplesForDeceleration", "diff", Color.yellow);
        _graph.AddChannel("removedSamplesForAcceleration", "diff", Color.red);
        _graph.AddChannel("jitterLatency", "raw", Color.white);

        // use the first available mic, if any
        if (Microphone.devices.Length > 0) {
            _audioDeviceName = Microphone.devices[0];
        }
        Debug.Log(string.Format("audioDeviceName='{0}'", _audioDeviceName));

        // create the Communicator
        _communicator = gameObject.AddComponent<HiFi.HiFiCommunicator>() as HiFi.HiFiCommunicator;
        _communicator.InputAudioDeviceName = _audioDeviceName;

        // for this test we want to fail ASAP whenever there is a problem
        // so we configure the retry/reconnect logic to not try hard
        HiFi.HiFiConnectionAndTimeoutConfig config = new HiFi.HiFiConnectionAndTimeoutConfig();
        config.AutoRetryConnection = false;
        config.AutoReconnect = false;
        _communicator.ConnectionConfig = config;

        // register handlers for various _communicator events
        _communicator.ConnectionStateChangedEvent += HandleHiFiConnectionStateChange;
        _communicator.PeerDataUpdatedEvent += HandlePeerChanges;
        _communicator.PeerDisconnectedEvent += HandlePeerDisconnects;

        if (string.IsNullOrEmpty(HiFiUrl)) {
            HiFiUrl = "wss://api.highfidelity.com:443/";
        }
        if (string.IsNullOrEmpty(HiFiJwt)) {
            HiFiJwt = "get your own Java Web Token (JWT) from https://account.highfidelity.com/dev/account";
            HiFiJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhcHBfaWQiOiIxNTU3Zjg1Ny1kOWQ5LTRhYzctOGFjYy1hM2IwNmY2MDhhNmQiLCJ1c2VyX2lkIjoiYW5kcmV3Iiwic3BhY2VfaWQiOiI4YWNhZDk1ZS1mZWI2LTQwNzMtYjdjZi1iYTJmMDVmNzFlZTIiLCJzdGFjayI6ImF1ZGlvbmV0LW1peGVyLWFwaS1ob2JieS0wMSJ9.e4LpUo6WLGlKHquuwSjrxscZ31t5wtW-VnoH7IMS71w";
        }

        #if USE_HIFI_COORDINATE_FRAME_UTIL
        // When using HiFiCoordinateFrameUtil we must compute the transforms once,
        // before trying to use them.
        HiFi.HiFiCoordinateFrameUtil.ComputeTransforms2D();
        #endif
    }

    void Start() {
        Debug.Log("Tester.Start");

        // verify HiFiUrl and HiFiJwt
        if (HiFiJwt == "get your own Java Web Token (JWT) from https://account.highfidelity.com/dev/account") {
            Debug.Log("ERROR: this demo needs a valid JWT before it will work!");
            QuitDemo();
        }

        // set misc Communicator config options
        _communicator.SignalingServiceUrl = HiFiUrl;
        _communicator.JWT = HiFiJwt;
        _communicator.UserDataStreamingScope = HiFi.HiFiCommunicator.UserDataScope.All;

        // start the communicator
        _communicator.ConnectToHiFiAudioAPIServer();

        // to help debug webrtc audio latency we register a callback for inbound audio stats
        _communicator.InboundAudioStatsHandler = HandleInboundAudioStats;

        // start the clock
        _clock = new System.Diagnostics.Stopwatch();
        _clock.Start();
    }

    void Update() {
        if (Input.GetKeyUp(KeyCode.Escape) || Input.GetAxis("Cancel") > 0.0f) {
            QuitDemo();
        }
        if (Input.GetKeyUp(KeyCode.M)) {
            // toggle mute
            _muteTheMic = !_muteTheMic;
            _communicator.InputAudioMuted = _muteTheMic;
        }
        #if EXPERIMENTAL_DEVELOPMENT
        if (Input.GetKeyUp(KeyCode.N)) {
            // swap between first two mics
            // to test ability to hot-swap devices
            var devces = Microphone.devices;
            if (devices.Length > 1) {
                if (_audioDeviceName == devices[1]) {
                    _audioDeviceName = devices[0];
                } else {
                    _audioDeviceName = devices[1];
                }
                Debug.Log(string.Format("switching to audioDevice='{0}'", _audioDeviceName));
                _communicator.InputAudioDeviceName = _audioDeviceName;
            }
        }
        #endif
        if (Input.GetKeyUp(KeyCode.G)) {
            // dump all stats
            _communicator.DumpAllStats();
        }
        if (Input.GetKeyUp(KeyCode.B)) {
            // dump only audio stats
            _communicator.DumpAudioStats();
        }
        if (Input.GetKeyUp(KeyCode.R)) {
            _communicator.DumpReceiverStats();
        }
    }

    void FixedUpdate() {
        double time = _clock.Elapsed.TotalSeconds;
        if (time > _updateUserDataExpiry) {
            ComputeNewPositionAndOrientation(time);
            _updateUserDataExpiry = time + _updateUserDataPeriod;

            // HiFi audio mixing expects movement in XZ plane,
            // but Unity 2D motion is in XY, so we transform the axes
            Vector3 p = Position;
            Quaternion q = Orientation;
            #if USE_HIFI_COORDINATE_FRAME_UTIL
                // This is how to do it using HiFiCoordinateFrameUtil
                _communicator.UserData.Position = HiFi.HiFiCoordinateFrameUtil.UnityPositionToHiFi(p);
                _communicator.UserData.Orientation = HiFi.HiFiCoordinateFrameUtil.UnityOrientationToHiFi(q);
            #else
                // This is how to do it manually (and more efficiently)
                // since all expected rotations are about the "up" axis
                _communicator.UserData.Position = new Vector3(p.x, 0.0f, -p.y);
                _communicator.UserData.Orientation = new Quaternion(0.0f, q.z, 0.0f, q.w);
            #endif
        }
    }

    void QuitDemo() {
        _communicator.DisconnectFromHiFiAPIServer();
        #if UNITY_EDITOR
            // Application.Quit() does not work in the editor so
            // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    void ComputeNewPositionAndOrientation(double time) {
        // move the user in a circle about the origin
        double theta = 2.0 * Math.PI * (time / (double)_orbitPeriod);
        // default Unity 2D motion is in XY plane (e.g. Camera is looking along +Z-axis)
        Position.x = _orbitRadius * (float)(Math.Sin(theta));
        Position.y = _orbitRadius * (float)(Math.Cos(theta));
        Position.z = 0.0f;
        Orientation.Set(0.0f, 0.0f, 0.0f, 1.0f);
    }

    void HandleHiFiConnectionStateChange(HiFi.HiFiCommunicator.AudionetConnectionState state) {
        // print the state to verify this method is being called
        Debug.Log(string.Format("Tester.HandleHiFiConnectionStateChange state={0}", state));
    }

    void HandlePeerChanges(List<HiFi.IncomingAudioAPIData> peers) {
        // this is where we would harvest data about: new peers,
        // and changes to known peers (e.g. volume, position, etc)
    }

    void HandlePeerDisconnects(SortedSet<string> keys) {
        // this is where we would forget about known peers who have left
    }

    void HandleInboundAudioStats(string stats_string) {
        Debug.Log(string.Format("Tester.HandleInboundAudioStats stats='{0}'", stats_string));
        // the stats should look soemthing like
        // '{"type":"inbound-rtp","id":"RTCInboundRTPAudioStream_3093915876","timestamp":1631308754349856,"ssrc":3093915876,
        // "isRemote":false,"mediaType":"audio","kind":"audio","trackId":"RTCMediaStreamTrack_receiver_11",
        // "transportId":"RTCTransport_audio_1","codecId":"RTCCodec_audio_Inbound_111","packetsReceived":1474,
        // "fecPacketsReceived":0,"fecPacketsDiscarded":0,"bytesReceived":230509,"headerBytesReceived":46816,
        // "packetsLost":-1,"lastPacketReceivedTimestamp":13254.927,"jitter":0,"jitterBufferDelay":52377.6,
        // "jitterBufferEmittedCount":1371840,"totalSamplesReceived":1357600,"concealedSamples":4000,
        // "silentConcealedSamples":3480,"concealmentEvents":1,"insertedSamplesForDeceleration":997,
        // "removedSamplesForAcceleration":18748,"audioLevel":6.103701895199438e-05,"totalAudioEnergy":1.565369392263879e-07,
        // "totalSamplesDuration":28.45000000000165}'
        //
        // Note: the online docs suggest timestamp is in millisec, however it appears to be microsec.
        try {
            JSONNode stats = JSON.Parse(stats_string);
            string[] keys = {"insertedSamplesForDeceleration", "removedSamplesForAcceleration", "concealedSamples"};
            foreach (string key in keys) {
                _graph.AddValue(key, (float)stats[key]);
            }

            // measuring current jitterDelay takes extra work
            ulong jbec = stats["jitterBufferEmittedCount"];
            float denominator = (float)(jbec - _lastJitterBufferEmittedCount);
            if (denominator == 0.0f) {
                denominator = 1.0f;
            }
            _lastJitterBufferEmittedCount = jbec;

            double jbd = stats["jitterBufferDelay"];
            float latency = (float)(jbd - _lastJitterBufferDelay) / denominator;
            _lastJitterBufferDelay = jbd;

            _graph.AddValue("jitterLatency", latency);

        } catch (Exception e) {
            Debug.Log(string.Format("adebug failed to handle stats err='{0}'", e.Message));
        }
    }
}
