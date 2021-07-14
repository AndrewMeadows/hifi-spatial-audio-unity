using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundAudioControl : MonoBehaviour
{
    public AudioSource backgroundAudio;
    public UnityEngine.UI.Toggle toggle;
    static BackgroundAudioControl _backgroundAudioControl;

    void Awake() {
        if (_backgroundAudioControl == null) {
            _backgroundAudioControl = this;
            DontDestroyOnLoad(_backgroundAudioControl);
        } else {
            Destroy(gameObject);
        }
    }

    public void Toggle(bool foo) {
        if (foo) {
            backgroundAudio.Play();
        } else {
            backgroundAudio.Pause();
        }
    }
}
