# Main menu: the dark and the moving light

**Date:** 2026-09-18
**Status:** implemented — see "As built" at the end

## Goal

Replace the inherited AIR lobby menu in `Assets/Game/Scenes/Core/MainMenu.unity` with a single
ominous screen that offers one action: start the fight.

The screen is near-black. A spotlight hangs above a fog-swallowed patch of ground and its pool of
light follows the mouse, finding boulders and trees at the rim as it sweeps. `ECLIPSE` is written
across the top but is legible only where the pool passes under it. `START GAME` sits centre-low,
dim but never invisible.

Bootstrap already routes to this scene: `Bootstrapper` loads build index 0, then build index 1,
which is `MainMenu.unity`. No change is needed there.

## Decisions taken

| Decision | Chosen | Rejected |
| --- | --- | --- |
| What the light hides | Title revealed by the light; button always carries a floor alpha | Light reveals everything; light is pure decoration |
| What the light is | A real URP spotlight in the 3D scene | A flat UI glow sprite on a black canvas |
| What it falls on | Ground plane, fog, and existing low-poly rocks and trees at the rim | Bare floor; the player character |
| Quitting | Dropped entirely — `QuitGame()` is deleted | Escape with a hint; Escape silently; a second QUIT button |

Quitting is gone on purpose. The screen offers exactly one thing, and leaving an unbound
`QuitGame()` behind would be dead code, which this project forbids. A built game is exited with the
platform's own quit.

## Architecture

Four new files under `Assets/Game/Scripts/Presentation/UI/`, namespace `SpaceGame.Presentation`.

The split exists to satisfy the project's second non-negotiable: every decision is a pure function,
and the MonoBehaviours only read input and write transforms.

### `Menu/SpotlightAim.cs` — static, pure

```
Vector3 Resolve(Ray cursorRay, Plane floor, Vector3 setCentre, float maxRadius)
Vector3 Follow(Vector3 current, Vector3 target, float timeConstant, float deltaTime)
```

`Resolve` intersects the cursor ray with the floor plane and clamps the hit into a circle of
`maxRadius` around `setCentre`. The clamp is load-bearing, not defensive: as the cursor nears the
horizon the ray approaches parallel with the floor and the intersection runs away to hundreds of
metres, which would swing the light off the set and black the screen out. A ray that does not hit
the plane at all (pointing up) returns the clamped point in the ray's horizontal direction rather
than falling back to centre, so the light keeps travelling the way the cursor is moving instead of
snapping home.

`Follow` is an exponential approach rewritten to be frame-rate independent:
`current + (target - current) * (1 - exp(-deltaTime / timeConstant))`. A raw `Lerp(a, b, k)` is
not — it converges faster at high frame rates, so the light would feel different on different
machines.

### `Menu/RevealField.cs` — static, pure

```
float Alpha(Vector2 cursor, Vector2 element, float innerRadius, float outerRadius, float floorAlpha)
```

Returns `1` within `innerRadius`, `floorAlpha` beyond `outerRadius`, and a smoothstep between.
Monotonically non-increasing with distance. `floorAlpha` is clamped into `0..1` and the result never
drops below it.

### `Menu/CursorSpotlight.cs` — MonoBehaviour

The only `Update()` in the feature. Reads `Mouse.current.position`, builds a ray through the menu
camera, calls `SpotlightAim.Resolve` then `SpotlightAim.Follow`, and writes the light's position and
`LookAt`. Holds no rules of its own.

Serialized: menu camera, floor height, light height, `maxRadius`, `followTimeConstant`. No magic
numbers. `setCentre` is not serialized separately — it is the component's own
`transform.position` flattened to the floor height, so moving the rig in the scene moves the
circle the light is allowed to roam.

If `Mouse.current` is null (no mouse attached) the light holds its last position rather than
snapping to centre, and the component does not log per frame.

### `Menu/LightRevealedGraphic.cs` — MonoBehaviour

Drives a `CanvasGroup.alpha` from `RevealField.Alpha`, using the element's own screen position so
the reveal tracks the element rather than a hard-coded point. `MainMenuCanvas` is
`ScreenSpaceOverlay`, so that position is `RectTransformUtility.WorldToScreenPoint(null, rect)` and
the cursor needs no camera conversion.

Serialized: `innerRadius`, `outerRadius`, `floorAlpha`.

Two instances: on the title with `floorAlpha = 0`, on the button with `floorAlpha ≈ 0.25`.

The button's `CanvasGroup` keeps `interactable` and `blocksRaycasts` true at all times. The alpha is
presentation only — a player who clicks where the button is must always get a click, whatever the
light is doing.

### `Pages/MainMenuUI.cs` — trimmed

`StartGame()` and `EnterWorld()` stay unchanged; the `LoadingScreenUI.ShowUntilReady` route into
`Arena` already works. `QuitGame()` is deleted.

The scene's serialized `minigameScene` and `worldConfig` fields are stale leftovers with no
counterpart in the script; they are dropped when the scene is re-saved in the Editor.

## Scene work

Done in the Editor over the Unity MCP connection, not by hand-editing YAML — the project's carve-out
notes ask for exactly that.

**Remove:** `Title` ("Space Game"), `ButtonRow` and its four `Menu Button` instances, `Cube.033`,
`LobbyPreviewAnchor`.

**Reframe:** `LobbyCameraView` → `MenuCamera`, repositioned to look down at the set, clear colour
black.

**Add:**
- A dark ground plane.
- Roughly six props at the rim from `ThirdParty/BugWarNature`: `LPW_RockBoulder`, `LPW_RockWall`,
  `LPW_Tree_*`. Existing prefabs; no new art is authored.
- A `Spotlight` GameObject carrying `CursorSpotlight`.
- Fog, and bloom on the `Global Volume` prefab instance already in the scene.

**Canvas** (`MainMenuCanvas` is kept): an `ECLIPSE` title, and one button cloned from
`Assets/Game/Prefabs/UI/Buttons/Menu Button.prefab`. That prefab carries `UIButton`, so hover and
press sounds and the state animator come for free rather than being re-implemented — the existing
`menuButtonPrefab` field on `MainMenuUI` already points at it.

## Game Development Constitution

- **`GDC-L1-FEEL-0002`** — *Acknowledge input immediately* (objective, confidence 5). The damped
  follow is the live risk: a slow lerp is indistinguishable from input latency, and the principle's
  exception (commitment as part of the fantasy) does not cover a menu lamp. Mitigation is a tight
  default — `followTimeConstant ≈ 0.06 s` — serialized so it can be tuned rather than argued about.
  Attached to the cursor, with just enough weight to read as a heavy light.
- **`GDC-L1-UX-0003`** — *Make the interface communicate* (objective, confidence 4) and
  **`GDC-L1-UX-0004`** — *Affordances and signifiers* (contextual, confidence 4). "If players have
  to squint, hunt, or guess, the UI has failed its job." This is the reason the button keeps a floor
  alpha and the title does not: decoration may hide, the one available action may not. UX-0003's
  stated exception permits deliberate minimalism *provided it still communicates*, and the floor
  alpha is what keeps this design on the right side of that line.
- **`GDC-L1-UX-0007`** — *Minimize friction between the player and the fun* (contextual,
  confidence 4). One screen, one button, straight into the fight. The light qualifies as the
  principle's *meaningful* friction (tone-setting) rather than incidental tax — but only because it
  never gates the click.
- **`GDC-L1-AUDIO-0005`** — *Use silence and dynamic range* (contextual, confidence 4). Noted and
  deliberately out of scope. A low drone under this scene would carry it harder than any visual
  decision above; worth a follow-up.

## Tests

EditMode, in `Assets/Game/Tests/EditMode/`, alongside the existing suite.

`SpotlightAimTests`
- A ray straight down at the floor resolves to the point below it.
- A near-parallel ray is clamped to `maxRadius` from `setCentre`, not flung to the horizon.
- A ray pointing away from the floor resolves in the ray's horizontal direction, still clamped.
- `Follow` converges monotonically toward the target.
- `Follow` over one step of `dt` and two steps of `dt/2` land within tolerance of each other —
  the frame-rate independence claim.

`RevealFieldTests`
- Alpha is `1` at the cursor and within `innerRadius`.
- Alpha equals `floorAlpha` beyond `outerRadius`.
- Alpha is monotonically non-increasing as distance grows.
- Alpha never drops below `floorAlpha`, including when `floorAlpha` is `0` or `1`.
- `innerRadius >= outerRadius` degenerates to a hard cutoff rather than dividing by zero.

## Out of scope

- Audio for the menu (see AUDIO-0005 above).
- The `Arena` scene itself, the player prefab, and `SpawnManager`'s prefab field — the three
  re-wiring jobs the carve-out notes list. Starting the game will load `Arena`; whether `Arena` is
  playable is a separate piece of work.
- Settings, credits, or any second screen.

## As built (2026-09-18)

The code landed as designed. The scene differs from the plan in three ways, all decided while
looking at the actual scene rather than its YAML.

**The set is a forest clearing, not a bare floor with six props.** Opening the scene revealed a
whole AIR lobby still in it — a `DemoScene` desert-ruin set of roughly sixty meshes, three
`AstronautArmature` rigs, a `Sun` directional light, a duplicate `EventSystem`, and a missing script
on the camera. All of that was removed. In its place: a ground plane and sixteen props from this
project's `BugWarNature` set — seven trees ringing the clearing at the fog line, four rocks at the
mid distance, five bracken ferns close in where the pool finds them first.

**Values, after two tuning passes against a rendered frame.** The first pass was almost invisible:
inverse-square falloff over the light's drop costs about two orders of magnitude, so an intensity
that reads as a lamp at arm's length reads as nothing at eleven metres.

| | Planned | Built |
| --- | --- | --- |
| Light height | 9 | 11 |
| Intensity | 14 | 900 |
| Spot angle | 52° / 16° inner | 75° / 25° inner |
| Fog density | 0.045 | 0.022 |
| Ground albedo | 0.055 | 0.21 |
| Follow time constant | 0.06 s | 0.06 s (unchanged) |

The props were also pulled inward: the pool is only about eight metres across, so anything beyond
that is never found however far the cursor sweeps.

**The spotlight carries a fixed downward rotation in the scene.** `CursorSpotlight` aims itself
every frame, but at edit time it would otherwise sit at identity and point along +z, so anyone
opening the scene would see a grazing smear instead of a pool.

### Outstanding

`QuitGame()` was deleted as agreed, then restored by another session working in this repository at
the same time, with a fuller implementation (`GameSettings.Save`, editor-aware quit). That version
was left alone. Nothing in the menu binds it, so it is currently unreachable — either bind it or
remove it, but it should not stay as it is.
