using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyFollower : MonoBehaviour {
    public MyBumper target;
    //int foo = 0;

    MyBumper _target;

    void Start() {
    }

    void FixedUpdate() {
        /*
        ++foo;
        if (0 == (foo % 50)) {
            Vector3 cpos = transform.position;
            Vector3 tpos = Vector3.zero;
            if (_target != null) {
                tpos = _target.transform.position;
            }
            string msg = string.Format("adebug camera=<{0},{1},{2}> target=<{3},{4},{5}>",
                    cpos.x, cpos.y, cpos.z, tpos.x, tpos.y, tpos.z);
            Debug.Log(msg);
        }
        */
    }

    public void SetTarget(MyBumper myBumper) {
        _target = myBumper;
    }
}
