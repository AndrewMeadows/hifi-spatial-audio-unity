using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterSoundControl : MonoBehaviour
{
    public AudioSource waterAudio;
    public UnityEngine.UI.Toggle toggle;
    static WaterSoundControl _waterSoundControl;

    void Awake() {
        if (_waterSoundControl == null) {
            _waterSoundControl = this;
            DontDestroyOnLoad(_waterSoundControl);
        } else {
            Destroy(gameObject);
        }
    }

    public void Toggle(bool foo) {
        if (foo) {
            waterAudio.Play();
        } else {
            waterAudio.Pause();
        }
    }
}
