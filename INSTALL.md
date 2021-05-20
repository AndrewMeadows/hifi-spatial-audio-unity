# Install package
Eventually it will be possible to install via github URL: https://github.com/highfidelity/hifi-spatial-audio-unity.git.
In the meantime you must obtain this package as a tarball or by file when unfurled onto local filesystem.

## Dependencies
Modified com.unity.webrtc which exposes the default audio device directly to webrtc rather than go through Unity's spatialized 3D audio subsystem:
https://github.com/highfidelity/com.unity.webrtc.git#hifi-spatial-audio

NativeWebsocket:
https://github.com/endel/NativeWebSocket.git#upm

SimpleJSON is included in this package.  Note: this will cause a conflict if you've already added SimpleJSON through some othe means.
