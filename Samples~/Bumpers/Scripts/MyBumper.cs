using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MyBumper : MonoBehaviour {
    public float LinearSpeed;
    public float MaxLinearSpeed;

    public float AngularSpeed;
    public float MaxAngularSpeed;

    public float MovementTimescale;

    float _targetAngularSpeed;
    string _input;
    Rigidbody2D _body;
    AudioListener _listener;
    MyControls _myControls;

    void Awake() {
        _listener = gameObject.AddComponent<AudioListener>() as AudioListener;
        _body = GetComponent<Rigidbody2D>();
        _myControls = new MyControls();
    }

    void Start() {
        // don't forget to enable the various input actions
        // which are disabled by default
        _myControls.Movement.ForwardBack.Enable();
        _myControls.Movement.Rotate.Enable();
        _myControls.Movement.Strafe.Enable();

        const float MIN_MOVEMENT_TIMESCALE = 0.05f;
        const float MAX_MOVEMENT_TIMESCALE = 1.0f;
        MovementTimescale = Mathf.Clamp(MovementTimescale, MIN_MOVEMENT_TIMESCALE, MAX_MOVEMENT_TIMESCALE);

        if (LinearSpeed <= 0.0f) {
            const float DEFAULT_LINEAR_SPEED = 4.0f;
            LinearSpeed = DEFAULT_LINEAR_SPEED;
        }
        if (MaxLinearSpeed <= 0.0f) {
            const float DEFAULT_MAX_LINEAR_SPEED = 10.0f;
            MaxLinearSpeed = DEFAULT_MAX_LINEAR_SPEED;
        }

        if (AngularSpeed <= 0.0f) {
            const float DEFAULT_ANGULAR_SPEED = 90.0f; // omfg degrees!
            AngularSpeed = DEFAULT_ANGULAR_SPEED;
        }
        if (MaxAngularSpeed <= 0.0f) {
            const float DEFAULT_MAX_ANGULAR_SPEED = 270.0f;
            MaxAngularSpeed = DEFAULT_MAX_ANGULAR_SPEED;
        }

        // use an artificially large moment of inertia
        // to reduce rotational consequences of collisions
        _body.inertia = 100.0f;
    }

    void FixedUpdate() {
        UpdateVelocities();
    }

    void Update() {
        UpdateCamera();
    }

    void UpdateVelocities() {
        // compute targetLinearVelocity based on input
        Vector2 targetLinearVelocity = LinearSpeed * new Vector2(
                _myControls.Movement.Strafe.ReadValue<float>(),
                _myControls.Movement.ForwardBack.ReadValue<float>());

        // transform world-velocity into local-frame
        Vector3 v = Quaternion.Inverse(transform.rotation) * new Vector3(_body.velocity.x, _body.velocity.y, 0.0f);
        Vector2 localLinearVelocity = new Vector2(v.x, v.y);

        // blend targetLinearVelocity into current velocity in the local-frame
        float del = Time.fixedDeltaTime / MovementTimescale;
        if (del > 1.0f) {
            del = 1.0f;
        }
        Vector2 newLinearVelocity = del * targetLinearVelocity + (1.0f - del) * localLinearVelocity;

        // clamp newLinearVelocity and apply it
        float newSpeed = newLinearVelocity.magnitude;
        if (newSpeed > MaxLinearSpeed) {
            newLinearVelocity *= MaxLinearSpeed / newSpeed;
        }

        // rotate back into world-frame
        v.Set(newLinearVelocity.x, newLinearVelocity.y, 0.0f);
        v = transform.rotation * v;
        _body.velocity = new Vector2(v.x, v.y);

        // similar blend for angular, except it is one-dimensional
        float targetAngularVelocity = AngularSpeed * _myControls.Movement.Rotate.ReadValue<float>();
        float newAngularVelocity = del * targetAngularVelocity + (1.0f - del) * _body.angularVelocity;
        if (System.Math.Abs(newAngularVelocity) > MaxAngularSpeed) {
            newAngularVelocity *= MaxAngularSpeed / System.Math.Abs(newAngularVelocity);
        }
        _body.angularVelocity = newAngularVelocity;
    }

    void UpdateCamera() {
        Camera camera = Camera.main;
        Vector3 newPosition = camera.transform.position;
        newPosition.x = transform.position.x;
        newPosition.y = transform.position.y;
        camera.transform.position = newPosition;
        camera.transform.rotation = transform.rotation;
    }
}
