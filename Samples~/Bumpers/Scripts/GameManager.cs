#define USE_HIFI_COORDINATE_FRAME_UTIL
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class GameManager : MonoBehaviour {
    public MyBumper myBumper;
    public OtherBumper otherBumper;
    public string HiFiUrl;
    public string HiFiJwt;
    public UnityEngine.UI.Toggle toggleGain;
    public UnityEngine.UI.Toggle toggleNoiseThreshold;
    static GameManager _gameManager;

    MyBumper _myBumper;
    System.Random _random;
    HiFi.HiFiCommunicator _communicator;

    Dictionary<string, OtherBumper> _others;

    void Awake() {
        if (_gameManager == null) {
            _gameManager = this;
            DontDestroyOnLoad(_gameManager);
        } else {
            Destroy(gameObject);
        }
#if USE_HIFI_COORDINATE_FRAME_UTIL
        // When using HiFiCoordinateFrameUtil we must compute the transforms once,
        // before trying to use them.
        HiFi.HiFiCoordinateFrameUtil.ComputeTransforms2D();
#endif
    }

    void Start() {
        _communicator = gameObject.AddComponent<HiFi.HiFiCommunicator>() as HiFi.HiFiCommunicator;

        // we can configure the communicator to retry on failure and also to reconnect
        HiFi.HiFiConnectionAndTimeoutConfig config = new HiFi.HiFiConnectionAndTimeoutConfig();
        config.AutoRetryConnection = true;
        config.AutoReconnect = true;
        _communicator.ConnectionConfig = config;

        _myBumper = Instantiate(myBumper) as MyBumper;
        _random = new System.Random();

        float radius = 7.0f;
        float theta = (float)(2.0 * System.Math.PI * _random.NextDouble());
        Vector2 direction = new Vector2((float)System.Math.Sin(theta), (float)System.Math.Cos(theta));
        _myBumper.transform.position = radius * direction;

        _others = new Dictionary<string, OtherBumper>();

        if (string.IsNullOrEmpty(HiFiUrl)) {
            HiFiUrl = "wss://api.highfidelity.com:443/";
        }
        if (string.IsNullOrEmpty(HiFiJwt)) {
            HiFiJwt = "get your own Java Web Token (JWT) from https://account.highfidelity.com/dev/account";
        }

        // TODO: get your own valid JWT above
        if (HiFiJwt == "get your own Java Web Token (JWT) from https://account.highfidelity.com/dev/account") {
            Debug.Log("ERROR: this demo needs a valid JWT before it will work!");
        #if UNITY_EDITOR
            // Application.Quit() does not work in the editor so
            // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
        } else {
            _communicator.PeerDataUpdatedEvent += HandlePeerChanges;
            _communicator.PeerDisconnectedEvent += HandlePeerDisconnects;
            _communicator.ConnectionStateChangedEvent += HandleHiFiConnectionStateChange;
            _communicator.SignalingServiceUrl = HiFiUrl;
            _communicator.JWT = HiFiJwt;
            _communicator.UserDataStreamingScope = HiFi.HiFiCommunicator.UserDataScope.Peers;
            _communicator.ConnectToHiFiAudioAPIServer();
        }
        _communicator.ConnectToHiFiAudioAPIServer();
    }

    void Update() {
        if (Input.GetKeyUp("escape") || Input.GetAxis("Cancel") > 0.0f) {
        #if UNITY_EDITOR
            // Application.Quit() does not work in the editor so
            // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
        }
    }

    void FixedUpdate() {
        // HiFi audio mixing expects movement in XZ plane,
        // but Unity 2D motion is in XY, so we transform the axes
        Vector3 p = _myBumper.transform.position;
        Quaternion q = _myBumper.transform.rotation;
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

    void HandlePeerChanges(List<HiFi.IncomingAudioAPIData> peers) {
        foreach(HiFi.IncomingAudioAPIData peer in peers) {
            string key = peer.visitIdHash;
            OtherBumper other;
            // Note: transform from y-forward to z-forward
#if USE_HIFI_COORDINATE_FRAME_UTIL
            // This is how to do it using HiFiCoordinateFrameUtil
            Vector3 p = HiFi.HiFiCoordinateFrameUtil.HiFiPositionToUnity(peer.position);
            Quaternion q = HiFi.HiFiCoordinateFrameUtil.HiFiOrientationToUnity(peer.orientation);
#else
            // This is how to do it manually
            Vector3 p = new Vector3(peer.position.x, -peer.position.z, 0.0f);
            Quaternion q = new Quaternion(0.0f, 0.0f, peer.orientation.y, peer.orientation.w);
#endif
            if (_others.TryGetValue(key, out other)) {
                other.SetTargetPosition(p);
                other.transform.rotation = q;
            } else {
                other = Instantiate(otherBumper) as OtherBumper;
                other.SetKey(key);
                other.SetTargetPosition(p);
                other.transform.position = p;
                other.transform.rotation = q;
                _others.Add(key, other);
            }
        }
    }

    void HandlePeerDisconnects(SortedSet<string> keys) {
        foreach (string key in keys) {
            OtherBumper other;
            if (_others.TryGetValue(key, out other)) {
                _others.Remove(key);
                other.Die();
            }
        }
    }

    void HandleHiFiConnectionStateChange(HiFi.HiFiCommunicator.AudionetConnectionState state) {
        Ravi.Log.UncommonEvent(this, "HiFiCommunicator state={0}", state);
        // Do special handling for Connection state change here
        // for example: we used to trigger a reconnection here on failure
        // however now HiFiCommunicator can be configured to reconnect on its own.
    }

    public void MuteAudioWithGain(bool foo) {
        bool muted = toggleGain.isOn;
        _communicator.InputAudioMuted = muted;
    }

    public void SetOtherGain(float gain) {
        foreach(KeyValuePair<string, OtherBumper> entry in _others) {
            string visitIdHash = entry.Key;
            bool success = _communicator.SetOtherUserGainForThisConnection(visitIdHash, gain);
            break;
        }
    }
}
