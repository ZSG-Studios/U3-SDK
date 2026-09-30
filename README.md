# U3 SDK — Unity 6.7 port

This fork of [SmartlyDressedGames/U3-SDK](https://github.com/SmartlyDressedGames/U3-SDK) contains the Unity **6000.7.0b2** upgrade, **URP 17.7**, Windows **DirectX 12/Vulkan**, Unity **AI Navigation**, repaired graphics/display controls, and external diagnostics with automated verification tools. This targets a beta editor; see [port notes and validation limits](UNITY_PORT.md).

The original [SDK license](LICENSE.txt) and [third-party notices](THIRDPARTYNOTICES.txt) apply. Steam game assets are loaded from your own Unturned installation and are not included in this fork.

Source code for [Unturned](https://smartlydressedgames.com/unturned/), a free open-world zombie survival sandbox game.

## Getting Started

1. Download/clone this repository
2. Install [Unity Hub](https://unity.com/download) (required to install engine)
3. Use the Unity **6000.7.0b2** editor (the installed version this port targets)
4. *Optional*: if making code changes, select **Game development with Unity** + **.NET desktop development** in the Visual Studio installer
5. Ensure Steam is running and you have [Unturned](https://store.steampowered.com/app/304930/Unturned/) installed (large binary files and mods are loaded from here)
6. Open the project with the Unity editor
7. Open the `Assets/GameStartup.unity` scene
8. Click play!

## Resources

- [Unity 6 port, tooling, and validation](UNITY_PORT.md)

- [Frequently Asked Questions](https://docs.smartlydressedgames.com/en/stable/u3-sdk/faq.html)
- [Source Code Demo: Adding a Heat-Seeking Missile on YouTube](https://youtu.be/CqJnkcWfmEY)
- [Unturned's Modding Documentation](https://docs.smartlydressedgames.com/en/stable/)
