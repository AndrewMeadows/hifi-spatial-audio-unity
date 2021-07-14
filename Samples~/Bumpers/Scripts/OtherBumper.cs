using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OtherBumper : MonoBehaviour {
    Vector3 _targetPosition;
    float _timescale = 0.1f;
    const float RADIANS_TO_DEGREES = 180.0f / (float)System.Math.PI;
    Rigidbody2D _body;

    void Start() {
        _body = GetComponent<Rigidbody2D>();
        _targetPosition = transform.position;

        // Use an artificially large inertia to avoid rotation consequences of collisions
        _body.inertia = 100.0f;
        _body.WakeUp();
    }

    void FixedUpdate() {
        // move toward target position
        Vector3 offset = _targetPosition - transform.position;
        float distance = offset.magnitude;
        Vector2 targetVelocity = Vector2.zero;
        bool needsAdjustment = _body.IsAwake();

        const float MAX_OFFSET = 2.0f;
        const float CLOSE_ENOUGH = 0.01f;
        if (distance > MAX_OFFSET) {
            needsAdjustment = true;
            transform.position = _targetPosition;
        } else if (distance > CLOSE_ENOUGH) {
            needsAdjustment = true;
            targetVelocity = (1.0f / _timescale) * new Vector2(offset.x, offset.y);
        }

        if (needsAdjustment) {
            float speedAdjustment = (targetVelocity - _body.velocity).magnitude;
            const float MIN_SPEED_ADJUSTMENT = 0.01f;
            if (speedAdjustment > MIN_SPEED_ADJUSTMENT) {
                // blend toward target velocity
                float del = Time.fixedDeltaTime / _timescale;
                if (del > 1.0f) {
                    del = 1.0f;
                }
                _body.velocity = del * targetVelocity + (1.0f - del) * _body.velocity;
            }
        }
    }

    public void SetTargetPosition(Vector2 position) {
        _targetPosition = position;
    }

    public void SetKey(string key) {
        SpriteRenderer renderer = gameObject.GetComponent(typeof(SpriteRenderer)) as SpriteRenderer;
        if (renderer != null) {
            // use the key to generate a random bright color
            int hash = key.GetHashCode();
            float phase = 0.5f * (1.0f + (float)hash / (float)System.Int32.MaxValue);
            float red = InkFromPhase(phase);
            float green = InkFromPhase(phase + 1.0f / 3.0f);
            float blue = InkFromPhase(phase + 2.0f / 3.0f);
            renderer.color = new Color(red, green, blue);
        }
    }

    static float InkFromPhase(float phase) {
        phase = phase % 1.0f;
        // Here is the the angle curve
        //     |
        //   1 +-------                               --------+
        //     |       \                             /
        //     |        \                           /
        //     |         \                         /
        //     |          \                       /
        //     |           \                     /
        //     |            \                   /
        //     |             \                 /
        //   0-+-------|------|=======|=======|-------|-------|--
        //     0      1/6    1/3     1/2     2/3     5/6      1
        if (phase < 1.0f / 6.0f || phase > 5.0f / 6.0f) {
            return 1.0f;
        }
        if (phase > 1.0f / 3.0f && phase < 2.0f / 3.0f) {
            return 0.0f;
        }
        if (phase < 1.0f / 3.0f) {
            return 1.0f - 6.0f * (phase - 1.0f / 6.0f);
        }
        return 6.0f * (phase - 2.0f / 3.0f);
    }

    public void Die() {
        Destroy(gameObject);
    }
}
