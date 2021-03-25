# hifi-spatial-audio-unity

## How to make unity demo

1. Open Unity-hub
2. Create fresh Unity project with Unity-2019.4 LTS
3. In Project tab create these folders:
    - Assets
    - Plugins
4. Add NativeWebSocket plugin
    1. Open Unity Window-->Package_Manager
    2. + button (upper left) --> Add_package_from_git_URL...
    3. enter: https://github.com/endel/NativeWebSocket.git#upm
5. Add SimpleJSON plugin
    1. Download from here: https://raw.githubusercontent.com/Bunny83/SimpleJSON/master/SimpleJSON.cs
    2. Copy SimpleJSON.cs into your Project/Plugins directory
6. Add Microsoft-MixedReality-Webrtc plugin
    1. Download the latest tarball from https://github.com/microsoft/MixedReality-WebRTC/tags
    2. Open Unity Window-->Package_Manager
    3. + button (upper left) --> Add_package_from_tarball...
    4. Select downloaded tarball
7. The hifi-spatial-audio-unity project is not yet bundled into a clean plugin and must be installed manually:
    1. Clone this repo to local disk
    2. Copy all `Plugins/*.cs` files to your `Project/Plugins/` directory
    3. Copy all `Scrips/*.cs` files to your `Project/Scripts/` directory
8. Drag `Project/Scripts/TestHiFiSession.cs` onto Camera object
9. Edit TestHiFiSession.cs to connect to your test HiFi-audio-mixer URL and custom JWT
10. Run demo
