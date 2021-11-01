# HiFi Spatialized Audio for Unity

<img src="https://img.shields.io/badge/unity-2019.4-green.svg?style=flat-square" alt="unity 2019.4">

This package provides access to [HiFi Spatialized Audio](https://docs.highfidelity.com/js/latest/index.html) in Unity.

## Installation

Please see [Install package](INSTALL.md).

## Usage overview:
1. Create an account: https://account.highfidelity.com/dev/account
1. Create a HiFi "App" for whatever project you're workining on.
1. Create a "Space" inside the App where Users can meet.
1. For each User that will be connecting to the Space: create a JSON Web Token (JWT).  The JWT is a compressed binary blob with info about the App Space and the User's publicly visible name (aka optional User ID).  Note: since the User's publicly visible name is optional it is technically possible to use a single JWT for all Users in the Space, however then all HiFi peers' `IncomingAudioAPIData` will have unique `visitHashID` but the same `providedUserID`.
1. In the Unity project:
    1. Add **com.endel.nativewebsocket** package via git URL: https://github.com/endel/NativeWebSocket.git#upm
    1. Add Unity's experimental **com.unity.webrtc** package version 2.4.0-exp.4 via git URL: https://github.com/Unity-Technologies/com.unity.webrtc.git#e5ef0b98f893d44b88863db39b700e71d19462f0
    1. Add this **com.highfidelity.spatialized-audio** package via git URL: https://github.com/HighFidelity/hifi-spatial-audio-unity.git
    1. Create a HiFi.HiFiCommunicator for the User.
    1. Set the `HiFiCommunicator.SignalingServiceUrl` to `wss://api.highfidelity.com:8001/`.
    1. Set the `HiFiCommunicator.JWT`
    1. Call `HiFiCommunicator.ConnectToHiFiAudioAPIServer();`
    1. Every frame set `HiFiCommunicator.UserData.Position` and `.Orientation` in the HiFi-frame.  Note the HiFi world coordinate frame is right-hand Cartesian with **forward=z-axis** and **up=y-axis**.  The Unity project will undoubtedly be using a different coordinate system and values must be transformed into the HiFi-frame before setting `HiFiCommunicator.UserData`.  There is a `HiFiCoordinateFrameUtil` class to help with this.

## Using the Bumpers Sample

1. Import the dependency packages first.
2. Add the hifi-spatial-audio-unity package [https://github.com/highfidelity/hifi-spatial-audio-unity.git](INSTALL.md).
3. Import the Bumpers Sample from the package. (e.g., select the HiFi Spatialized Audio package in the Package Manager => Samples => Bumpers => Import)
4. From the Project window, add Assets => Samples => HiFi Spatialized Audio => 0.2.0 => Bumpers => Scenes => BumpersScene to the Hierarchy, and remove any other scenes.
5. In the "BumpersScene" Hierarchy, select gameManager. In the Inspector, click on the empty text box next to "Hi Fi Jwt". Enter a JWT generated from https://account.highfidelity.com
6. Set up an Input Axis Rotate if it doesn't already exist, using Edit => Project Setting... => Input Manager => Axes.
Increase size to get a new field. Change values:
```
  Name: Rotate
  Negative Button: d
  Positive Button: a
  Alt Negative Button: right
  Alt Positive Button: left
  Gravity: 3
  Sensitivity: 3
  Snap: (checked)
  Type: Key or Mouse Button
  Axis: X axis
```
7. Alternatively to steps 5 and 6: Outside of the Unity Editor: copy the sample's `CopyToInputManager.asset` file on top of your `ProjectSettings/InputManager.asset` file (e.g. copy `.../unityProject/Assets/Samples/Hifi Spatialized Audio/0.2.0/Bumpers/CopyToInputManager.asset` to `.../unityProject/ProjectSettings/InputManager.asset`).

## Licenses

- [LICENSE.md](LICENSE.md)
- [Third Party Notices.md](Third_Party_Notices.md)

## Contribution
- [CONTRIBUTING.md](CONTRIBUTING.md)
