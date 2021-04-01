# HiFi Spatialized Audio for Unity

<img src="https://img.shields.io/badge/unity-2019.4-green.svg?style=flat-square" alt="unity 2019.4">

This package provides access to [HiFi Spatialized Audio](https://www.highfidelity.com/api) in Unity.
ATM It is only available for Unity 2019.4.xxx because it depends on the com.unity.webrtc plugin.

## Installation

Please see [Install package](INSTALL.md).

## Usage overview:
1. Create an account: https://account.highfidelity.com/dev/account
1. Create a HiFi "App" for whatever project you're workining on.
1. Create a "Space" inside the App where users can meet.
1. For each User that will be connecting to the Space: create a Java Web Token (JWT).  The JWT is a compressed binary blob with info about the App Space and the User's publicly visible name (optional User ID).
1. In the Unity game:
    1. Create a HiFiCommunicator.
    1. Set the `HiFiCommunicator.SignalingServiceUrl`
    1. Set the `HiFiCommunicator.JWT`
    1. Call `HiFiCommunicator.ConnectToHiFiAudioAPIServer();`
    1. Every frame set `HiFiCommunicator.UserData.Position` and `.Orientation`

## Licenses

- [LICENSE.md](LICENSE.md)
- [Third Party Notices.md](Third_Party_Notices.md)

## Contribution
- [CONTRIBUTING.md](CONTRIBUTING.md)
