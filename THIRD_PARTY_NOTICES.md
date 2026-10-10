# Third-party notices

The root MIT license applies to original SwapHunter code, documentation, procedural geometry and synthesized audio. It does not relicense third-party materials or dependencies.

| Component | License / source |
|---|---|
| Noto Sans SC Regular | SIL Open Font License 1.1; license retained at `Game/Assets/_SwapHunter/Fonts/OFL.txt`; source: https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/SC |
| Three.js 0.186.1 | MIT; original notice retained at `Game/Assets/_SwapHunter/Art/ThreeModels/THREE-LICENSE.txt`; installed via the locked npm dependencies |
| Unity Editor, URP, Input System, AI Navigation, glTFast and other Unity packages | Their respective Unity/package licenses apply; versions and sources are pinned in `Game/Packages/manifest.json` and `packages-lock.json`. Editor binaries and package caches are excluded |
| Unity template/project settings | Retain applicable Unity terms; these configuration files are included to reproduce the project |

Detailed resource provenance is retained in [Game/THIRD_PARTY_NOTICES.md](Game/THIRD_PARTY_NOTICES.md).

Original project-authored geometry and synthesized sound were created for this project with AI assistance. The D1.1 CC0 additions are separately listed below and in THIRD_PARTY_ASSETS.md. No Counter-Strike installed files, code, maps, models, textures or audio are included.

Noto Sans SC font copyright notice: © 2014–2021 Adobe (http://www.adobe.com/). The bundled font retains its original metadata and SIL OFL license.


## v0.5 additional original assets and footage

- `combat-knife-v05`: original Blender geometry authored by this project; generator, editable `.blend`, GLB and manifest are included in `Tools/BlenderAuthoring`.
- `swaphunter-key-art-v05.png`: original AI-generated concept illustration made with the built-in image-generation tool on 2026-09-27, with no third-party reference image supplied. It is concept art, not a gameplay screenshot.
- New gameplay clips are captured from this project's Unity player. Automatic inputs and preconfigured Boss mechanism demonstrations are labeled in the video and storyboard. No third-party game footage is used.
- Trailer audio uses the game's original procedural sound bank and PhaseScore, reconstructed from emitted sound-event timing and remixed for the trailer. It is not a hardware-loopback recording. Noto font and existing dependency notices above continue to apply.


## D1.1 adopted CC0 visuals and development dependency

See [the actual asset ledger](THIRD_PARTY_ASSETS.md) and its JSON file for exact acquired files, hashes, original licences and permitted payloads. Quaternius supplies five CC0 environment meshes; Kenney supplies three CC0 particle textures. Their original geometry/textures are not project-authored. Project-authored fitting, cues and original actor/display variants retain the project licence. The evaluated George mech is not adopted.

Unity MCP v10.3.0 is included as a local UPM package at Dependencies/com.coplaydev.unity-mcp, extracted byte-for-byte from the official archive of commit aa5fc638d623f56178d50329d9ad8c541a57fe66. Its repository MIT license (CoplayDev, 2025) is retained in the package and Dependencies/Licenses/unity-mcp-MIT.txt. Embedded Tommy is separately copyright Denis Zhidkikh (2020), MIT; its original header and Dependencies/Licenses/Tommy-MIT.txt are retained. The repository Server subtree, Library/package caches and credentials are not included. Dependencies/unity-mcp-source.json records the archive and selected file hashes, plus the previous Git declarations. Existing Unity, Noto and Three.js notices continue to apply.
