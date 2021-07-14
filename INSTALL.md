# Install package

In the Package Manager (Unity editor Window => Package Manager):

Import the dependencies, below. (E.g., click + => Add package from git URL..., and specify the URL).

Import https://github.com/highfidelity/hifi-spatial-audio-unity.git.

Once imported, there is also a Sample named Bumpers.


## Dependencies
Modified com.unity.webrtc which exposes the default audio device directly to webrtc rather than go through Unity's spatialized 3D audio subsystem:
https://github.com/highfidelity/com.unity.webrtc.git#hifi-spatial-audio

NativeWebsocket:
https://github.com/endel/NativeWebSocket.git#upm

SimpleJSON is included in this package.  Note: this will cause a conflict if you've already added SimpleJSON through some other means.
