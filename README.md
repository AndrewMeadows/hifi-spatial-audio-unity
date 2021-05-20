# HiFi Spatialized Audio for Unity

<img src="https://img.shields.io/badge/unity-2019.4-green.svg?style=flat-square" alt="unity 2019.4">

This package provides access to [HiFi Spatialized Audio](https://www.highfidelity.com/api) in Unity.

## Installation

Please see [Install package](INSTALL.md).

## Usage overview:
1. Create an account: https://account.highfidelity.com/dev/account
1. Create a HiFi "App" for whatever project you're workining on.
1. Create a "Space" inside the App where Users can meet.
1. For each User that will be connecting to the Space: create a Java Web Token (JWT).  The JWT is a compressed binary blob with info about the App Space and the User's publicly visible name (aka optional User ID).  Note: since the User's publicly visible name is optional it is technically possible to use a single JWT for all Users in the Space, however then all HiFi peers' `IncomingAudioAPIData` will have unique `visitHashID` but the same `providedUserID`.
1. In the Unity project:
    1. Add **com.endel.nativewebsocket** package via git URL: https://github.com/endel/NativeWebSocket.git#upm
    1. Add Highfidelity's modified **com.unity.webrtc** package via git URL: https://github.com/AndrewMeadows/com.unity.webrtc.git#hifi-spatial-audio
    1. Add this **com.highfidelity.spatialized-audio** package.
    1. Create a HiFi.HiFiCommunicator for the User.
    1. Set the `HiFiCommunicator.SignalingServiceUrl` to `wss://api.highfidelity.com:8001/`.
    1. Set the `HiFiCommunicator.JWT`
    1. Call `HiFiCommunicator.ConnectToHiFiAudioAPIServer();`
    1. Every frame set `HiFiCommunicator.UserData.Position` and `.Orientation`.  Note the HiFi world coordinate frame is right-hand Cartesian with **forward=z-axis** and **up=y-axis**.  If the Unity project is using a different coordinate system then values must be transformed into the HiFi world frame before setting `HiFiCommunicator.UserData`.

## Licenses

- [LICENSE.md](LICENSE.md)
- [Third Party Notices.md](Third_Party_Notices.md)

## Contribution
- [CONTRIBUTING.md](CONTRIBUTING.md)
