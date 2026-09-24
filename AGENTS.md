<!-- pathrule:begin (managed by Pathrule; edits inside this region are overwritten) -->
<!-- Pathrule managed — do not edit; cloud state is authoritative. -->

# Pathrule integration

Pathrule is this workspace's shared memory, rule, and skill layer for AI agents.
Codex should use it as a smart reminder system, not a full-context dump.

## Context Policy

- Pathrule is the first knowledge layer for this workspace. Use hook context first: metadata, relevant rule titles, path reminders, and filename/skill matches.
- `::skill-name` is a hard gate: use the exact injected skill; if missing, stop and resolve it through Pathrule/MCP before file edits.
- Do not reflexively call `pathrule_get_context` before every small known-path code task. For discovery, inventory, architecture, recent activity, or "list/show/find/where/which" prompts (Turkish: listele, göster, bul, nerede, hangi, neler), call it before any grep/read/fallback when hook context is missing, ambiguous, or stale.
- Hook silence on a topic does not mean Pathrule has no relevant memory/rule. For discovery/inventory/architecture questions, call `pathrule_get_context` first; fall back to files, git, or general knowledge only after Pathrule returns nothing relevant.
- When calling `pathrule_get_context`, pass `cwd`, `user_intent`, and `omit_protocol: true`; the companion file already contains the protocol.
- Read full bodies with `pathrule_read_memory`, `pathrule_read_rule`, or `pathrule_read_skill` when a surfaced title/id is relevant.
- Treat existing local edits as protected user/team work: inspect overlaps, never revert unrelated changes, and keep edits scoped.
- Obey every surfaced rule. If a user request conflicts with a Pathrule rule, warn before taking action.

## Writes

- Pathrule cloud is the source of truth. Do not create local memory files such as `MEMORY.md` or `~/.claude/memory/`.
- Use path-first writes for lasting project knowledge: `pathrule_write_memory`, `pathrule_write_rule`, and `pathrule_write_skill` take the most specific workspace-relative `node_path`.
- Do not edit materialized local Pathrule files as the source of truth. This includes `.agents/skills/**/SKILL.md`, legacy `.codex/skills/**/SKILL.md`, `.claude/skills/**`, rendered companion files, and synced skill/memory/rule files. Read only for orientation; create/update cloud records with the Pathrule MCP write/update tools.
- After any file-modifying response, call `pathrule_log_activity` once with domain, action, scope, subjects, files_touched, and a concise task_summary.
<!-- pathrule:end -->

---

# AI Agent Quick Start

**Project**: Interactive aquarium exhibit (Unity 6 URP, `6000.0.64f1`)  
**Language**: C#, HLSL/ShaderLab  
**Key docs**: [CLAUDE.md](CLAUDE.md) (architecture & working practices), [Assets/Scripts/](Assets/Scripts/) (core implementation)  
**Validation**: No CLI pipeline — open in Unity Editor, play scene, watch Console.log  

## 30-second overview

Visitors color printed fish with QR codes. Scanner reads artwork → app decodes QR → extracts colors → generates texture → fish enters virtual tank with pre-made school. All business logic lives in `Assets/Scripts/`; behavior tuned via Inspector fields on `SampleScene.unity`, not code constants.

## Essential files (in order of importance)

| File | Purpose |
|------|---------|
| [Assets/Scripts/QRFolderScanner.cs](Assets/Scripts/QRFolderScanner.cs) | Central orchestrator: imports scans, decodes QR, creates textures, queues fish |
| [Assets/Scripts/BackgroundFishSpawner.cs](Assets/Scripts/BackgroundFishSpawner.cs) | Spawns background school, handles click/touch interactions, manages depth layers |
| [Assets/Scripts/PlayerFishTextureApplicator.cs](Assets/Scripts/PlayerFishTextureApplicator.cs) | Binds generated texture to fish, enforces alpha-clipped depth rendering |
| [Assets/Scripts/FishRandomMotion.cs](Assets/Scripts/FishRandomMotion.cs) | Per-fish movement logic (horizontal, seahorse, jellyfish, crab, starfish, shrimp) |
| [Assets/Scripts/DOTweenFishAnim.cs](Assets/Scripts/DOTweenFishAnim.cs) | Mesh deformation, water-entry animation, bubble trail |
| [Assets/Scenes/SampleScene.unity](Assets/Scenes/SampleScene.unity) | Main scene; all runtime config lives here (bounds, templates, prefabs, tint values) |
| [CLAUDE.md](CLAUDE.md) | Authoritative guide to architecture, naming conventions, and key decisions |

## Common workflows

### Adding a new fish species
1. Illustrate QR code payload: e.g., `ca_kiem` (Vietnamese)
2. Create prefab: name must match QR payload exactly (case-insensitive; `ca_kiem.prefab`)
3. Add alpha template (silhouette outline texture)
4. Register in Inspector: `QRFolderScanner` → **Fish Templates** array → new `FishTemplate`
5. Add default texture to `Assets/StreamingAssets/default_fish/` (e.g., `ca_kiem_01.png`)
6. Optionally add prefab to `BackgroundFishSpawner` → **Fish Prefabs** if it should appear in background school
7. Test: place color artwork in scanner folder → app should decode and spawn

**Critical naming rule**: `CanonicalFishId()` normalizes strings → lowercase, `-` / ` ` → `_`, strip trailing `_\d+`. So `Ca_kiem`, `ca-kiem`, and `ca_kiem_01` all map to `ca_kiem`.

### Editing fish behavior (spawn, motion, appearance)
- **First, check SampleScene.unity**: public fields on GameObjects override code defaults. Code-only edits often have no visible effect.
- **Motion**: Edit `FishRandomMotion.Configure()` call in `BackgroundFishSpawner.ConfigureRandomMotion()` or add species logic in `FishMotionSpecies` enum.
- **Appearance**: Edit material/shader properties via Inspector on prefabs or via `PlayerFishTextureApplicator.Apply()` parameters.
- **Water entry**: Tune `QRFolderScanner` → **Water drop effect** section (phase timing, overshoot, tilt, duration).

### Testing texture generation & QR decoding
1. Drop `.png`/`.jpg` with QR code into `D:\Images` (or configured `sourceFolderPath`)
2. Play scene, press `I` (import) or wait for `autoImportIntervalSeconds` to trigger
3. Check Console for `[QR]` logs: if QR decodes successfully, will show `Payload → prefab` and texture generation stats
4. Press `P` to release queued fish, or enable `autoReleaseFish` to spawn immediately
5. If fish doesn't appear: check alignment via `[FishTexture]` log (% colored pixels), or verify template crop rect

### Debugging depth/rendering issues
- **Fish invisible or washed out underwater**: verify `PlayerFishTextureApplicator` is forcing `_AlphaClip` + `ZWrite On`. Reverting to transparent breaks depth sampling.
- **Fish behind/in front of background incorrectly**: adjust `depthRange` on `BackgroundFishSpawner` or per-fish Z position
- **Click interaction not triggering**: check `FishClickInteraction.TryTriggerAtScreenPosition()` in `BackgroundFishSpawner.Update()` and raycast depth

## Key patterns & conventions

### Fish identity normalization
Used across `QRFolderScanner` and `BackgroundFishSpawner`:
```csharp
string id = CanonicalFishId("Ca_voi_1"); // → "ca_voi"
bool match = IsFishIdPrefix("ca_voi_blue", "ca_voi"); // → true
```
Keep QR payloads, prefab names, and texture filenames aligned under this rule.

### Texture composition pipeline
1. Load scan image → color pixels
2. Load alpha template → silhouette mask
3. Flood-fill silhouette alpha (line-art → solid shape) via `GetFilledSilhouetteAlpha()`
4. Auto-align crop rect if `autoAlignScannedArtwork` (finds ink bounds + matches to alpha bounds)
5. Paint scan colors through mask into output texture
6. Apply UV inset (only for `ca_voi` whale) to avoid edge seams

### Material depth workaround
Player fish require **private material instances** with `ZWrite On` and alpha-clipping. This ensures they write to the camera depth texture correctly for underwater post-processing. Never use `MaterialPropertyBlock` or transparent blending for player fish.

### Messaging conventions
All logs are tagged: `[QR]`, `[FishTexture]`, `[BackgroundFishSpawner]`, `[Default Fish]`, etc.  
Vietnamese used for user-facing messages (exhibit locale). English for debug/architecture notes.

## Common pitfalls

| Issue | Cause | Fix |
|-------|-------|-----|
| New fish species doesn't spawn | Prefab name doesn't match QR payload (canonicalized) | Rename prefab or update QR payload |
| Code change has no effect | Value overridden in Inspector on SampleScene.unity | Check GameObject fields first, or clear override |
| Fish invisible / washed out underwater | Material reverted to transparent (`ZWrite Off`) | Restore `PlayerFishTextureApplicator` logic for `_AlphaClip` + `ZWrite On` |
| Click/touch not registering | `interactionDepth` on `TouchOceanManager` wrong | Adjust to match fish spawn depth range |
| Default fish don't spawn in build | Using `Assets/default_fish` path (Editor-only); should use `Assets/StreamingAssets/default_fish` | `QRFolderScanner.defaultFishFolderPath` already uses `StreamingAssets` relative path—check it matches |
| Texture misaligned on fish | `scanCrop` rect wrong, or auto-align disabled | Enable `autoAlignScannedArtwork` or calibrate `scanCrop` per template |
| Import loop / infinite re-import | Files in `Executive_folder` triggering repeated re-processing | Check import ledger logic in `LoadImportLedger()` — verify signature is stable |

## When to edit scene vs. script

**Prefer Inspector tuning** for:
- Spawn bounds, swim lane count, fish sizes, emission intensity
- Template prefabs and alpha masks
- Depth ranges, camera settings
- Click/bubble particle behavior, timings

**Prefer code editing** for:
- Fish identity matching logic (`CanonicalFishId`, `IsFishIdPrefix`)
- Texture composition and alignment (pixel-level operations)
- New motion species (add to `FishMotionSpecies` enum, species-specific behavior in `FishRandomMotion`)
- Build/editor compatibility (#if guards, path resolution)
