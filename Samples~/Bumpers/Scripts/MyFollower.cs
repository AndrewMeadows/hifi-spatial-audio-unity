using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyFollower : MonoBehaviour {
    public MyBumper target;

    MyBumper _target;

    void Start() {
    }

    void FixedUpdate() {
    }

    public void SetTarget(MyBumper myBumper) {
        _target = myBumper;
    }
}
