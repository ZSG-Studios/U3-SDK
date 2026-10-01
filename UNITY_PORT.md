# Unity 6 port

This checkout targets the installed **6000.7.0b2 (d6546dc2b3a9)** beta editor on Windows.
Upstream was Unity 2022.3.62f3. This port uses **URP 17.7**, native RenderGraph, and native Volume components.
The old Built-In renderer and Post Processing v2 package have been replaced.
Windows uses **DirectX 12 first, Vulkan second**, with automatic API selection disabled.
DirectX 11 and OpenGL are excluded from Windows players. Launch flags explicitly select each backend during verification.
The native Windows Build Profile selects DXC for DirectX 12; Vulkan uses Unity's default compiler.

## Changes

- Updated packages to the editor's bundled versions: Burst 2.0.0, Collections 6.7.0, Editor Coroutines 6.6.0, uGUI 2.7.0, and Test Framework 1.9.0.
- Updated Visual Studio integration to 2.0.28 and Memory Profiler to the latest stable 1.1.12.
- Removed the deprecated TextMesh Pro shim. Essential resources resolve from the package owning `TMP_Text`, and use the current package-import API.
- Modernized fragment outputs to `SV_Target`. The resource importer also upgrades the old shader debug pragma and fragment semantics in the installed TextMesh Pro resource archive.
- Applied Unity's API updater for rigidbody velocity/damping, physics material names, and light fade properties.
- Used full `EntityId` keys in prefab pools and material/attachment caches, and in component ordering.
- Updated editor assembly enumeration to `CurrentAssemblies.GetLoadedAssemblies()` and scripting defines to `NamedBuildTarget`.
- Added the Sprite Mask engine module and removed the deleted Game Center callback from the unused-system list.
- Migrated Bundle Tool to `BuildAssetBundles` with a unique staging folder, Windows 64-bit output, and the selected destination filename preserved.
- Added Forward+ and Deferred+ renderer assets, native projected decals, SSR, GTAO, bloom, color adjustment, grain, vignette, and depth of field.
- Ported authored clothes, skins, cosmetics, wind, terrain blending, rain, snow, sky, water fallback, and particle equations to explicit URP HLSL passes. Terrain normal-map slots retain their Unturned snow-mask meaning.
- Converted project materials and added a runtime adapter for Steam-loaded Standard/legacy materials. Texture transforms, colors, normal/specular/emission maps, workflow, cutout, fade, and premultiplied transparency are preserved by focused verification.
- Added a Resources shader catalog with explicit source-asset references. Steam bundles contain shaders with identical names; resolving project references first prevents their legacy shaders from overriding the URP ports.
- Replaced camera image callbacks and immediate-mode geometry with native RenderGraph atmosphere, scope crop/blur/vignette, and reusable gizmo meshes. World cameras use Forward+ or Deferred+; viewmodels use a Forward+ overlay.
- Replaced offscreen camera rendering with `RenderPipeline.SubmitRenderRequest`. Reflection faces render to a depth-backed 2D target before a native SRP Blitter copy into the cubemap with matching orientation, avoiding URP 17.7's depthless cubemap intermediate.
- Replaced foliage drawing with `Graphics.RenderMesh` and `Graphics.RenderMeshInstanced`, and the old camera-command-buffer comparison component with a native RenderGraph comparison.
- Migrated development checks to `UNITY_ENABLE_CHECKS`, profiling to `UNITY_INCLUDE_INSTRUMENTATION`, and build variants to `ManagedCodeVariant`. Removed retired player-loop types and marked runtime-only fields nonserialized.
- Embedded the installed SRP Core package to repair its missing Pipeline attribute assembly reference and expose the official RenderGraph CLI. Limited beta fixes also address shader backend limits, unsigned BVH masks, and invalid DXC output in an editor-only Dynamic GI probe kernel. See `Packages/com.unity.render-pipelines.core/U3-SDK-PATCH.md` for the fixes and the probe-kernel optimization workaround.
- Embedded URP 17.7 with limited decal fixes: initialize GBuffer outputs, mask optional MRT writes, create the DBuffer clear material only for DBuffer rendering, and retain a valid no-keyword clear-shader variant. See `Packages/com.unity.render-pipelines.universal/U3-SDK-PATCH.md`.
- Restored active render textures and detached camera targets before returning temporary textures to the pool during icon, atlas, satellite, and reflection captures.

## Tooling

Unity CLI **1.0.0-beta.11** is installed; `com.unity.pipeline` **0.8.0-exp.1** connects it to the Editor.
The project has Unity's official `unity-cli` and `unity-pipeline` skills under `.agents/skills/`.
The CLI skill is also installed under the user's Codex skills directory and becomes available on the next turn.
The publishing machine registered `unity mcp` and Unity's public Issue Tracker MCP.
Machine-local Codex configuration is excluded from this fork. After installing Unity CLI,
register this checkout using your own absolute project path:

```powershell
codex mcp add unity -- unity mcp --project-path (Get-Location).Path
codex mcp add unity-issue-tracker --url https://issuetracker-mcp.unity.com/mcp
```

The CLI's Issue Tracker auto-configurator does not support Codex; use the explicit registration above.

The connected Editor exposes **178 MCP tools**, including 16 RenderGraph commands; the Issue Tracker exposes **5**.
These catalogs describe availability, not proof that every operation has been tested.
Runtime Pipeline is enabled for the development client used for local validation. Keep it disabled in distribution builds.
Per-process discovery descriptors contain credentials and are ignored by Git.

```powershell
# Run from this project directory. Steam and its installed Unturned assets are required.
unity open . --args '-force-d3d12 -automated -NoWorkshopSubscriptions'
unity status --format json
unity command set_autotick --enable true
unity command open_scene --path Assets/GameStartup.unity
unity command editor_play

# Validate source with the installed editor (close the Editor first).
unity run . --timeout 600 --log-file Logs/import.log --no-tail -- -accept-apiupdate -nographics
unity test . --mode EditMode --timeout 600 --output Logs/editmode-results.xml

# Verify the actual MCP transport and save the current tool schemas.
uv run Tools/probe_unity_mcp.py
uv run Tools/probe_unity_mcp.py --issue-tracker

# Focused Bundle Tool fixture, including prefab dependencies and output filename.
unity command run_script --file Tools/VerifyBundleTool.cs --entry VerifyBundleTool.Main --timeout_ms 120000

# Verify imported shader passes and material semantics.
unity command run_script --file Tools/VerifyURPShaders.cs --entry VerifyURPShaders.Main --timeout_ms 180000 --timeout 190
unity command run_script --file Tools/VerifyURPMaterials.cs --entry VerifyURPMaterials.Main

# With Tutorial loaded in Play mode, exercise scopes and RenderGraph gizmos.
unity command run_script --file Tools/VerifyURPGameplay.cs --entry VerifyURPGameplay.Main --timeout_ms 60000 --timeout 70

# After building Builds/Windows64/Unturned.exe as a development player:
python Tools/smoke_client.py --graphics-api dx12
python Tools/smoke_client.py --graphics-api vulkan
python Tools/smoke_client.py --graphics-api dx12 --map PEI
python Tools/smoke_client.py --graphics-api vulkan --map PEI
```

The smoke runner is hidden, requests a 60 FPS limit, and has a 240-second watchdog. It temporarily
excludes Workshop subscriptions by launch flag, reaches Menu, starts Tutorial singleplayer,
checks for a loaded level and player with advancing frames, rejects console errors, and exits.
It also rejects warnings, unsupported shaders, unexpected legacy material subshaders, and native GPU/resource errors.
It renders the main camera to a temporary render texture for screenshots: occluded Windows
players can produce black screen captures. These images verify world rendering and exclude overlay HUD canvases.
It does not change Steam subscriptions. Local game saves and logs are under the ignored build directories.

## Evidence

- Current URP C# compilation: no errors or warnings in the stopped Editor.
- Shader verification checks active URP subshaders and binds every pass with weather, foliage, packed-normal, and smoothness keywords. It does not exhaust every keyword combination.
- Material verification: **13 assertions passed**, `Logs/urp-material-verification.json`.
- Editor reached Menu and Tutorial with a loaded player on the RX 9070 XT. The native camera stack uses one Base world camera and one Overlay viewmodel.
- Scope crop produced nonblack pixels; blur, vignette, and RenderGraph gizmo capture passed with **0 console errors/warnings**, `Logs/urp-gameplay-verification-2.json`.
- Both MCP transports initialized successfully; editor status, console status, and Issue Tracker search calls passed. The Issue Tracker search did not find the exact DBuffer assertion.
- The URP Bundle Tool fixture built and reloaded its prefab, material, and mesh dependencies: **77,376 bytes**, `Logs/urp-bundle-tool-verification.json`.
- DirectX 12 Editor: **73 shaders, 267 passes, 0 errors and warnings**, `Logs/urp-shader-verification-dx12.json`; scope and RenderGraph gizmo fixture passed, `Logs/urp-gameplay-verification-dx12.json`.
- Latest broad EditMode suite: **3,271 passed, 0 failed, 92 skipped**, `Logs/urp-editmode-final.json`. Ninety cases are explicitly disabled by Unity and two require Renderer2D. The corrected depth-copy fixture passes all nine cases (`Logs/urp-depth-copy-tests.json`). Skips are not passes.
- Cubemap fixtures: all six faces match native capture on **DX12 and Vulkan**, mean RGB byte error approximately **0.045**, `Logs/urp-reflection-verification-dx12.json` and `Logs/urp-reflection-verification-vulkan.json`.
- Vulkan Editor: **73 shaders, 267 passes, 0 errors**, including packed-normal/smoothness variants, `Logs/urp-shader-verification-vulkan.json`; scope and RenderGraph gizmo fixture passed, `Logs/urp-gameplay-verification-vulkan.json`.
- Native RenderGraph CLI captured a reflection camera with **28 passes, 27 active**, `Logs/urp-rendergraph-reflection-metrics.json`.
- The graphics/display repair build has **zero errors and warnings** (`Logs/graphics-settings-build-status.json`). Its focused player checks are `Logs/graphics-settings-smoke-dx12.json` and `Logs/graphics-settings-smoke-vulkan.json`. The earlier standalone baseline completed **20/20 cases**, covering all ten installed maps on both APIs, with **zero runtime errors and warnings** (`Logs/port-qualification.json`); that report identifies its earlier build. Built-In results are historical evidence only, preserved as `Logs/built-in-windows-build-status.json` and `Logs/built-in-standalone-smoke.json`.

This is a working development port to an installed **beta** editor, not a release qualification.
Multiplayer, every Workshop mod, and all maps have not been validated.
Legacy shader names and deprecated public SDK aliases are retained for existing asset/mod compatibility;
active rendering paths use URP APIs. This checkout preserves SDG's customized Steamworks wrapper rather
than replacing it blindly with upstream Steamworks.NET.
Existing Workshop subscriptions produced missing-prefab warnings in the first editor test; baseline validation
uses `-NoWorkshopSubscriptions` without changing subscriptions. The installed Unity Pipeline's `quit` command
fails in stopped Editor mode; `EditorApplication.Exit` closes the editor correctly. Player `quit` works.

## Water, navigation, and persistent diagnostics

The missing-water report reproduced at Germany's shoreline. The atmosphere RenderGraph pass
replaced the camera color handle while subsequent transparent draws were discarded from the
final result. It now blits the atmosphere back into the original camera attachment. A normal
shoreline capture with fog enabled shows the restored water (`Logs/water-world-fog-fixed.png`).
The isolated water test alone did not reproduce this full-scene failure.

Dry terrain uses the authored specular workflow with zero dry specular/smoothness; diffuse
alpha remains height-blending data. Rain adds its authored wet specular/smoothness. The terrain
depth-normal pass uses the same material equations. Shared PBR calls use the current explicit
URP lighting arguments.

Unity AI Navigation **2.0.15** replaces the SDK's empty navigation backend. Existing baked
navigation triangles are converted to native NavMesh data; zombie steering follows native paths
while retaining the game's CharacterController simulation. The small `AstarPathfindingProject`
assembly contains an original compatibility adapter for serialized `Pathfinding.NavmeshCut`
references, implemented with native carving obstacles. It includes no proprietary A* code.
Custom cut meshes use their bounds and dual cuts remain nonblocking; these are native obstacle
approximations rather than a promise of identical A* triangulation. Germany loaded 87 obstacles,
71,848 native triangles, and 34 zombies; all 34 sampled onto navigation geometry. Native AI player-loop phases are retained for
obstacle carving. The standalone Vulkan fixture follows a carved detour and verifies the
path reopens when its migrated blocker is disabled.

`Tools/Unturned/Upgrade installed map ambience and road bundles` rebuilds legacy map bundles
into a Unity-version and source-content-addressed local cache. It preserves complete texture
mips and imports self-contained decoded audio before rebuilding. The original Steam files remain
the source. `UNTURNED_ASSET_DIRECTORY` overrides the default Steam install path. Preparation runs automatically after Editor import, before Play, and before player builds.
Alternate Steam libraries are detected through Steam's library registry; the override is also used
by the runtime asset loader. Twenty bundle
references across ten installed maps reduced to twelve unique rebuilt bundles. Verification
compared 92 assets: all texture mip pixels matched exactly and audio sample differences stayed
below 0.00005 (`Logs/modern-map-bundle-verification.json`). Player builds copy the verified cache.

`PortDiagnostics` writes timestamped JSONL events and exact occurrence summaries into
`Logs/Diagnostics` beside the project or player. It captures Editor, Play, and player sessions,
samples repeated events without changing Unity's console, redacts credential diagnostics, and
writes scene/load/player/frame heartbeats every ten seconds. The latest Germany Editor session
recorded zero errors and zero warnings after navigation and bundle conversion. Earlier failing
sessions remain available, including the visible rerun's repeated menu null reference.

`Tools/VerifyRuntimeNavigation.cs` checks native obstacle carving, reopening, and actual SDK
movement. Focused DX12/Vulkan reruns also verify a lateral detour over four meters without
entering the blocked footprint (`Logs/standalone-smoke-dx12-germany.json` and
`Logs/standalone-smoke-vulkan-germany.json`). `Tools/VerifyURPWater.cs` compares water-enabled and water-disabled captures with the
atmosphere and an enabled overlay camera active. Both fixtures pass on DX12 and Vulkan;
the water comparison changes all 65,536 pixels. The standalone matrix is complete and passes.
This baseline uses installed Steam maps with Workshop subscriptions disabled; it does not
qualify arbitrary Workshop content or every gameplay interaction.

## Documentation

- [Unity CLI and commands](https://docs.unity.com/en-us/unity-cli/unity-cli-reference)
- [Unity Pipeline installation](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package)
- [Official Unity skills](https://github.com/Unity-Technologies/skills/tree/main/skills/unity-cli)
- [Unity 2022 LTS to Unity 6 upgrade guide](https://docs.unity3d.com/6000.0/Documentation/Manual/UpgradeGuideUnity6.html)
- [Unity 6.7 graphics API support](https://docs.unity.com/en-us/engine/6000.7/manual/platform-specific/cross-platform-features/graphics-apis)
- [Native URP render requests](https://docs.unity.com/en-us/engine/6000.6/manual/cameras/urp/multiple/user-render-requests)
- [DXC compiler and shader migration](https://docs.unity.com/en-us/engine/6000.7/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reducing/shader-dxc-compiler)
- [Managed code variants](https://docs.unity.com/en-us/engine/6000.7/manual/scripting/debugging-and-diagnostics/managed-code-variants)
- [U3-SDK startup requirements](https://github.com/SmartlyDressedGames/U3-SDK)
- [U3-SDK FAQ and bundled asset requirements](https://docs.smartlydressedgames.com/en/stable/u3-sdk/faq.html)

Use `unity docs <ClassOrMethod> --url` for documentation URLs matched to this checkout.
The installed editor also supplies API XML documentation in `Editor/Data/Managed/UnityEngine` and
`Editor/Data/Managed/UnityEditor`. These established the 6.7 assembly and entity-ID APIs where the web pages were unavailable.

Full strict player matrix (all ten installed maps on both APIs):

```powershell
python Tools/qualify_port.py
```

This rejects incomplete builds and nonzero build errors/warnings even when Unity reports
`Succeeded`, then runs each hidden player with its own watchdog. It writes a consolidated
`Logs/port-qualification.json` and individual native, CLI, JSONL, summary, and fixture logs.
A failed case remains failed in the report; it is not cleared or filtered from the console.

Launch the built DX12 game visibly on Germany and leave it running:

```powershell
python Tools/run_client.py
```

The launcher waits for completed asset loading before requesting the level, verifies an
advancing gameplay session, and saves `Logs/interactive-launch.json` plus the native log.
The player's automatic JSONL diagnostics continue while the game runs.

Raw master-bundle, content-reference/palette, Resources, and asynchronous asset loads now
apply the same material conversion as normal SDK bundle loads. These paths were responsible
for legacy airdrop and cardboard materials in the small maps.

Left-handed NPC bones and mirrored navigation boxes use equivalent convex collider meshes;
box geometry cannot support negative transforms in current PhysX. Six face raycasts and
world bounds match the original authored box. Physics materials, trigger state, collision
layer overrides, contact options, and generated-mesh cleanup are preserved.

URP's nonserialized camera history is disposed on Editor disable before domain reload can
replace its owner. This prevents persistent Scene View cameras leaking SSR history textures.
The focused enter/leave Play-mode resource test passes. Latest broad Editor results:
3,271 passed, zero failed, 92 skipped (`Logs/urp-editmode-final.json`). The skipped cases
are 90 explicitly disabled Unity cases and two Renderer2D cases. They are not reported as passes.

## Graphics and display controls

Lighting selects authored Off/Low/Medium/High/Ultra URP assets rather than applying Built-In
shadow settings to one shared asset. Presets use 0/100/200/300/400 meter shadow distances,
512/512/1024/2048/4096 main shadow maps, and 1/1/2/4/4 cascades; Off disables shadow support.
Native ambient occlusion features are installed in both renderers. AO Off explicitly overrides
intensity to zero, and SSR Off explicitly selects Disabled; inactive volume components would
otherwise fall back to the beta package's enabled defaults. SSR is available in both Forward+
and Deferred+. Post effects follow an enabled viewmodel camera, including perspective changes.
AA changes also update viewmodel data; the menu identifies TAA's SMAA fallback on a camera stack.

Display launch flags choose the initial window; later menu edits change resolution/fullscreen.
Resolution data retains the selected refresh rate numerator and denominator through JSON,
including fractional rates such as 60000/1001. Launch FPS caps appear in the menu, and typing
or toggling a cap takes precedence over the launch override. UI scale edits likewise replace
their initial override. Windowed FOV uses viewport aspect rather than monitor aspect, and
single-render scope textures resize after the end-of-frame window change.

`Tools/VerifyGraphicsSettings.cs` invokes the actual menu callbacks, renders all lighting
presets in both modes, checks AA/SSR/post effects and display bindings, and compares AO pixels
using isolated geometry. `smoke_client.py --map Germany --graphics-api dx12 --verify-graphics`
also resizes the actual window to 1024x768 and checks a 768-pixel single-render scope. Repeat
with `--graphics-api vulkan`. The harness restores settings and now checks warning calls in
the native log after shutdown. Culling volumes no longer reactivate destroyed objects after
their bundle materials have been unloaded during quitting.

`Logs/graphics-settings-qualification.json` records a passing build and both passing API cases:
zero build errors/warnings and zero runtime/shutdown warnings or errors. AO changed 188 pixels
in Forward+ and 210 in Deferred+ on each API. The actual 1024x768 window, 4:3 aspect, and resized
768-pixel scope were verified. The focused Editor resource check passed
(`Logs/graphics-settings-resource-test-retry.json`); its initial cold-run failure is preserved
as `Logs/graphics-settings-resource-test-before.json` rather than reported as a pass.

Sun-shaft and outline-quality implementations are omitted from the public SDK. Those inert
controls are disabled with availability tooltips; interaction highlighting retains the SDK
tint fallback. Their effects have not been implemented by this settings repair.

## Published validation snapshot (2026-09-30)

Raw logs, screenshots, Steam-derived bundles, build output, and per-process credentials stay local and are ignored by Git. The source includes the verification tools to repeat these checks on your installation.

| Scope | Result | Qualification |
| --- | --- | --- |
| Latest Windows development build (`bbee7281787c`) | Succeeded; 0 errors, 0 warnings | Includes the graphics/display fixes and quit cleanup. |
| Latest Germany smoke runs, DX12 and Vulkan | Both passed; 0 runtime errors/warnings and 0 native warnings including shutdown | Graphics presets, shadows, AA, SSR, AO off/on images, post effects, FPS, VSync, UI scale, rational refresh serialization, and a real 1024×768 window resize. |
| Earlier complete installed-map matrix | 20/20 passed; 10 maps × 2 APIs | Build `7748695e82d4`; predates the latest graphics/display fixes. |
| Earlier broad EditMode suite | 3,271 passed, 0 failed, 92 skipped | Predates the latest graphics/display fixes; skipped tests are not passes. |
| Focused resource-leak fixture | First cold run failed; unchanged warm retry passed 1/1 | The cold SSR depth-history failure remains recorded locally; the retry does not establish a new leak fix. |

Exclusive fullscreen, borderless transitions, and physical monitor refresh changes were not automatically exercised. Proprietary SDK-omitted effects remain unavailable, and the AI Navigation compatibility layer does not implement every A* Pro feature. These results do not establish full gameplay or release qualification.

Before publishing, five test-only subassets left in the embedded URP default volume profile by the upstream editor tests were removed together with their component references. Project runtime profiles were unaffected.

## Reproducing from GitHub

The committed `PortReproducibility.BuildWindowsDevelopment` entry point builds the native Windows profile without requiring a running Pipeline server or saved Editor preferences. Preparation creates missing Steam-derived bundles, preserves compressed texture mips through editor serialization, and validates cached bundle checksums, conversion revision, and Unity version before reuse. Build callbacks copy the cache and Steam app-ID file. No generated content is required from the publishing machine.

`Tools/reproduction-baseline.json` records the editor, Steam build ID, and SHA-256 hashes of the source map bundles. `Logs/project-preparation.json` records whether your inputs match it. A Steam update changes the inputs and must be qualified again; matching this receipt is not a promise of identical performance or images on different GPUs/drivers. Unity CLI, Python, and a Steam/Unity license remain external prerequisites for the automated checks.

The first conversion must read the original Steam archives. Unity emits native migration notices for their pre-2019 serialized formats during that conversion. These are preserved in the preparation log. Converted bundles use the current format; the runtime qualification rejects those notices in the player. The harness also checks native error/exception calls and importer/serialization diagnostics after shutdown, beyond the managed console counts.
