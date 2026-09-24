# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

This is a Unity 6 (`6000.0.64f1`) URP project for an interactive aquarium exhibit. Visitors color a printed fish template that has a QR code on it; the app scans the resulting artwork, decodes the QR, extracts the visitor's colored artwork, generates a fish texture from it, and drops an animated fish carrying that texture into a virtual tank alongside a school of pre-made background fish. Most in-repo comments and log strings are in Vietnamese, matching the exhibit's locale — keep new comments/logs consistent with that unless told otherwise.

There is no application source outside `Assets/` — this is a Unity Editor project, not a package/library with its own build tooling.

## Working with this repo

- There is no CLI build/lint/test pipeline in this repo (no npm/dotnet scripts, no `Assets/**/Tests` folder). All iteration happens by opening the project in the Unity Editor (`6000.0.64f1` — matching version required, see `ProjectSettings/ProjectVersion.txt`), pressing Play, and watching the Console.
- Runtime behavior is driven almost entirely by public fields Inspector-tuned on scene GameObjects (spawn bounds, prefab lists, templates, tint/emission values, etc.), not by code constants. When changing a script's defaults, check whether the corresponding scene object (`Assets/Scenes/SampleScene.unity`) already overrides that field — a code-only change may have no visible effect.
- `com.unity.test-framework` is a dependency but unused; there are no existing tests to model new ones on.
- Two scenes exist: `Assets/Scenes/SampleScene.unity` (the working aquarium scene) and `Assets/Scenes/Room_1.unity`.

## Architecture

### Fish pipeline (the core system)

`Assets/Scripts/QRFolderScanner.cs` is the central orchestrator and by far the largest script. Its pipeline:

1. **Import**: copies new images from an external folder (`sourceFolderPath`, e.g. a scanner's output directory) into `folderPath` (`Assets/Executive_folder` by default), tracking already-imported files via a signature ledger under `Application.persistentDataPath` so re-imports are skipped across sessions.
2. **Decode**: reads each queued image, decodes a QR code with ZXing (`Assets/Plugins/zxing.dll`), and matches the payload text to a `FishTemplate` (see below).
3. **Extract & align**: crops the visitor's colored artwork out of the scan. If `autoAlignScannedArtwork` is on, it locates the artwork's ink bounds and the template's alpha-silhouette bounds and computes a best-fit crop, instead of trusting a fixed `scanCrop` rect — scans are rarely perfectly registered.
4. **Composite**: flood-fills the template's alpha silhouette (`GetFilledSilhouetteAlpha`/`CalculateFilledSilhouetteAlpha`) to get a filled mask (the source alpha is just outline art), then paints the cropped scan colors through that mask into an output `Texture2D`.
5. **Bind & spawn**: hands the generated texture to `PlayerFishTextureApplicator.Apply(...)`, which builds private material instances on the prefab's renderers, and adds/configures a `DOTweenFishAnim` for swim behavior. The fish is queued (`pendingFish`) rather than shown immediately.
6. **Release**: pressing **P** dequeues and activates the next fish (`ReleaseNextFish`), which plays a water-drop-and-splash entrance (`DOTweenFishAnim.PrepareWaterDrop` / `StartPreparedWaterDrop`) into the tank. Pressing **I** triggers `ImportImagesNow()` to pull in new scans on demand.

`QRFolderScanner` also spawns a fixed "default fish" school at `Start()` from ready-made textures in `defaultFishFolderPath` (`default_fish`, resolved relative to `Application.streamingAssetsPath` — lives at `Assets/StreamingAssets/default_fish` in the project, which is the one folder Unity ships as loose files in a build; do not move it back under plain `Assets/`, that path only exists in the Editor and silently spawns zero default fish in a build) — these skip QR decoding entirely and use `FindTemplateFromFileName` to match a filename to a template instead (see `Assets/StreamingAssets/default_fish/README.txt` for the naming convention, e.g. `ca_heo_01.png`).

**Fish identity matching** is done by a canonicalized string comparison used across `QRFolderScanner` and `BackgroundFishSpawner` (`CanonicalFishId`/`IsFishIdPrefix`/`FindTemplate*`): lowercase, spaces/dashes to underscores, collapse repeated underscores, strip a trailing numeric suffix (so `Ca_voi_1` and `ca_voi` are the same fish). QR payload, prefab name, and default-texture filenames must all agree under this normalization — when adding a new fish species, name the prefab, its QR payload, and its texture file consistently (e.g. `ca_kiem`, `ca_kiem.prefab`, `ca_kiem_01.png`).

A `FishTemplate` (nested serializable class in `QRFolderScanner`) pairs a `qrId`, a `prefab`, an `alphaTemplate` (outline texture used as the fill mask), a `scanCrop` calibration rect, and `spriteFacesRight`.

### Rendering fish textures (`PlayerFishTextureApplicator.cs`)

Applies a generated texture to a fish's renderers via **private material instances** (not `MaterialPropertyBlock`), because the fish must participate correctly in the camera depth texture: source rig materials are transparent (`ZWrite Off`), which breaks an underwater full-screen post effect that samples depth. The applicator reconfigures each material for alpha-clipped opaque rendering (`_AlphaClip`, cutoff, forced `ZWrite On`, `DepthOnly` pass enabled) so the transparent-looking silhouette still writes correct depth. If you touch material/shader setup for fish, preserve this constraint — reverting to `ZWrite Off` will reintroduce the underwater visual bug this code works around.

### Fish swimming & background school

- `DOTweenFishAnim.cs` — per-fish swim state machine using DOTween: spawn scale-in or scripted water-drop entrance, then repeating `DOPath`/Catmull-Rom swim segments within `swimBounds` (or camera-derived bounds), with tilt applied from actual movement direction (not the nominal segment direction) and a "swim lane" mode to keep fish from bunching at screen center.
- `BackgroundFishSpawner.cs` — populates the full background school on `Start()` from prefabs in `fishPrefabs` matched against textures in `defaultTextureFolderPath`, distributing fish across swim lanes with a minimum-spacing search (`FindComfortablePosition`) and per-depth scale/speed falloff. It also owns several runtime-only nested classes defined in the same file (not separate assets): `JellyfishRiseAnim` (rise-and-glide motion for `ca_muc`), `SponsorFlagDisplay` (billboard sponsor flag attached to a fish's back), and `FishClickInteraction` (click/tap-to-scare with bubble-particle burst and trail, added to every spawned fish via `ConfigureClickInteraction`, which `QRFolderScanner` also calls for player fish it spawns).
- `WhalePatrol3D.cs`, `GodRayOrientation.cs` — standalone decorative 3D behaviors (whale patrol path, particle "god ray" orientation fixups for the current 2.5D camera setup); not wired into the fish-template pipeline.
- `StickerAnim.cs` — a simpler, self-contained wobble/bob/swim script; not used by the QR/background pipeline.

### Editor tooling

`Assets/Editor/MucdbUvPostprocessor.cs` is an `AssetPostprocessor` that regenerates planar UVs at import time for one specific FBX (`Assets/prefabs/fish/mucdb/mucdb.fbx`) whose exported UVs are degenerate. It also force-reimports that asset once on editor load if it's still missing usable UVs. This is a targeted fix for that one model, not a general import pipeline.

### Third-party dependencies

- **DOTween / DOTweenPro** (Demigiant) under `Assets/Plugins/Demigiant` — used for all tweened motion (fish paths, drop sequences, fades).
- **ZXing.Net** (`Assets/Plugins/zxing.dll`) — QR decoding; `QRFolderScanner.DecodeQrPixels` converts Unity's bottom-up `Color32[]` to the top-down RGB byte layout ZXing expects.
- URP 17.3, Input System 1.18 (new Input System with `#if ENABLE_INPUT_SYSTEM` guards throughout, falling back to the legacy manager under `#if ENABLE_LEGACY_INPUT_MANAGER`), 2D feature set, AI Navigation, Timeline, Visual Scripting, Vector Graphics — see `Packages/manifest.json` for the full dependency list.
