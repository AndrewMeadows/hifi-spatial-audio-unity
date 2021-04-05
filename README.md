# hifi-spatial-audio-unity

## How to make unity demo

1. Open Unity-hub
2. Create fresh 2D-template Unity project with Unity-2019.4 LTS
3. In Project tab create these folders:
    * Assets
    * Plugins
4. Add **NativeWebSocket** plugin
    1. Open Unity Window-->Package_Manager
    2. Click "+" button (upper left) --> Add_package_from_git_URL...
    3. enter: https://github.com/endel/NativeWebSocket.git#upm
5. Add **SimpleJSON** plugin
    1. Download from here: https://raw.githubusercontent.com/Bunny83/SimpleJSON/master/SimpleJSON.cs
    2. Copy SimpleJSON.cs into your Project/Plugins directory
6. Add **Unity-Technologies-Webrtc** plugin
    2. Open Unity Window-->Package_Manager
    3. Click "Advanced" combo box and check "Show Preview Packages" option.
    4. Search for "webrtc".
    5. Select "WebRTC preview -2.3.x-preview" package.
7. The **hifi-spatial-audio-unity** project is not yet bundled into a clean plugin and must be installed manually:
    1. Clone this repo to local disk
    2. Copy all `Plugins/*.cs` files to your `Project/Plugins/` directory
    3. Copy all `Scrips/*.cs` files to your `Project/Scripts/` directory
    4. Copy all `Scripts/Test/*.cs` files to `Project/Scripts/Test` directory
8. Drag `Project/Scripts/Test/TestHiFiSession.cs` onto the **Camera** GameObject
9. Edit `TestHiFiSession.cs` to connect to your test **HiFi-audio-mixer** URL and custom **JWT**
10. Run demo and watch the Console logs.  You should see the demo connect and start updating the user's position.
