# Eclipse

Unity 6 project (6000.3): a **single-player fighting game**. Carved out of the AIR codebase on
2026-09-18, which was itself carved out of SpaceGame. What is kept is a deliberately small
framework — the player character and camera, melee combat, health and damage, the enemy/AI stack,
the vegetation and terrain world, inventory and items, interaction, input, spawning and scene
management.

**The world is `Scenes/World/NatureWorld.unity`.** It is the game's main world, and the main menu's
Play button loads it. The terrain, the vegetation system under `Scripts/World/` and the
`BugWarNature` pack all exist to serve it — they are not AIR leftovers, whatever their history.

**The game is single-player and has no notion of a second machine.** The last remnants of the
netcode shape — the `Network` authority shim, the `NetMessaging` / `NetChannel` / `NetMsg` message
bus, `AgentAuthority` and the `Cosmetic` projectile flag — were deleted on 2026-09-18. What was
routed through a message id is a direct call now: a use goes `OnRequestUse` → `PlayUse` → `TryUse`
in `EquipmentController`, a blast on the player is `FlungBody.Fling`. The describe/do/present split
items are written in survives, because it is a gameplay shape rather than a netcode one:
`OnRequestUse` fills in a `UseContext`, `Use()` runs the effect and `Present()` plays the look.

Namespaces are still `SpaceGame.*` throughout. That is inherited, not a decision — renaming them
is a mechanical change nobody has made yet.

## What is here

| Area | Where | Entry points |
| --- | --- | --- |
| Player | `Scripts/Characters/Player/` | `PlayerController`, `Movement`, `PlayerLook`, `PlayerStance` |
| Melee combat | `Scripts/Characters/Player/Combat/`, `Scripts/Enemies/` | `PlayerMeleeSwing`, `MeleeSwingSequence`, `MeleeStrike` |
| Health / damage | `Scripts/Gameplay/Health/` | `HealthComponent`, `Damage`, `DamageFeedback` |
| Enemies / AI | `Scripts/Enemies/` | `EnemyAgent`, `EnemyBrain`, `EnemyBase`, `EnemyAlert` |
| World | `Scripts/World/` | `VegetationField`, `VegetationLayout`, `WorldAtmosphere` |
| Items | `Scripts/Items/` | `InventoryItem`, `UsableItem`, `UseContext`, `EquipmentController`, `HotbarController` |
| Interaction | `Scripts/Gameplay/Interaction/` | `Interactor`, `IInteractable`, `InteractableTrigger` |
| Spawning | `Scripts/Gameplay/Game/Spawning/` | `SpawnManager`, `SpawnPoint`, `SpawnClearance` |
| Teleporting | `Scripts/Core/Motion/Teleport.cs`, `Scripts/Core/Teleporting/` | `Teleport.Move`, `ITeleportAware` |
| Menu | `Scripts/Presentation/UI/` | `MainMenuUI`, `CursorSpotlight`, `RevealField` |
| Weapon wheel | `Scripts/Items/Inventory/Wheel/`, `Scripts/Presentation/UI/WeaponWheel/` | `WeaponWheel`, `RadialSelection`, `WeaponWheelView` |

Scenes, and all three are what is in Build Settings: `Scenes/Core/Bootstrap.unity`,
`Scenes/Core/MainMenu.unity` and the world, `Scenes/World/NatureWorld.unity`.

## What was removed, and what that leaves missing

The strip-down on 2026-09-18 took out everything AIR-specific plus every system this game does not
yet use. Gone: flight (ornithopter, wingsuit, wing pack), the grappling hook, mounts and riding, the
racing and minigame modes, NPC caravans and the Ostrich, save/load, the multiplayer stack, the
procedural legged-locomotion stack, the ragdoll system, all ranged combat (`Weapon`, `Projectile`,
`Magazine`), the player's aim rig and flashlight, the astronaut and nomad bodies, and the arena
scene.

**Audio is gone entirely** — FMOD, the banks and every `Sfx` call site. Nothing in the project makes
a sound, and the volume settings in `GameSettings` now feed nothing. Re-adding audio means choosing
a backend first, not restoring call sites.

**UI is the main menu and the weapon wheel, and nothing else.** There is no HUD: no crosshair, no
health bar, no death screen, no interaction prompt, no damage numbers, no pause menu. The wheel is
held on Q: the game slows, the look input steers a pointer round the dial instead of the camera, and
letting go equips the hotbar slot under it. It is built from code at runtime by `WeaponWheel`, which
sits on the player prefab, so there is no UI prefab or scene to keep in step with it. Q used to be
the flight-era Deploy action; nothing consumed it.

The player's animation clips under `Art/Animations/Player/` were kept deliberately — they are
reusable — even though the body they were imported for is gone.

## State of the project

Scripts compile clean with no reference to a deleted type, no prefab or scene holds a dangling
reference, and there are no orphan `.meta` files. `PlayerCharacter.prefab` is tagged `Player` and
carries `PlayerController`, `Movement`, `PlayerLook`, `PlayerStance`, `PlayerMeleeSwing`,
`ThirdPersonCameraBoom`, `HealthComponent`, `Interactor` and `EquipmentController`.

The player wears the Human (`Art/Models/Characters/Human/Human.fbx`), a Humanoid rig built from the
sculpt in `Art/Models/_Source~/models/characters/human_sculpt_base/`. It runs on the Goblin's
controller and clips, retargeted through the Humanoid avatar; the enemies are still the goblins. The
EditMode suite is green (123 tests).

Not wired up: `GoblinEnemy.prefab` and `GoblinCamp.prefab` survive but no longer appear in any
scene, because the arena that held them is gone. Drop them into `NatureWorld.unity` to get a fight
running again.

## Non-negotiables for every new feature

### 1. No code smells

Match the surrounding code and leave nothing for someone else to clean up:

- No dead code, commented-out blocks, or leftover debug logs.
- No copy-paste — if it exists in the codebase, reuse it; if it now exists twice, extract it.
- No god classes or catch-all managers; keep the module boundaries the codebase already has.
- No magic numbers — serialize tunables so they can be tuned in the Inspector.
- No empty or silent `catch`. Fail loudly or handle it properly.
- Names say what the thing *is*; no `Manager2`, `temp`, `doStuff`.

### 2. Logic out of MonoBehaviours where it can be

`SpaceGame.EnemyAI` and `SpaceGame.Vegetation` are testable without the Editor, and the EditMode
suite is the cheapest verification this project has. A decision buried in an `Update()` is a
decision nothing can test — `EnemyBrain` is the shape to copy, a plain class the MonoBehaviour
ticks.

## Skills

| Skill | Use it for |
| --- | --- |
| [spacegame-agent](.claude/skills/spacegame-agent/SKILL.md) | **Stale** — describes the deleted module/faction stack, not `Scripts/Enemies/`. See [docs/architecture/EnemySystem.md](docs/architecture/EnemySystem.md) |
| [blender-model](.claude/skills/blender-model/SKILL.md) | Any 3D asset — models, props, variants — in the `.blend` library |

Architecture notes live in [docs/architecture/](docs/architecture/). Both those and the skills were
inherited, so an example that names a feature this project no longer has (net gun, portals, mounts,
sandstorms, the ornithopter) is describing a pattern, not a file.

## Game design decisions — consult the Game Development Constitution

A vendored copy of the public **Game Development Constitution** (143 source-audited,
engine-agnostic principles across 24 domains, CC-BY-4.0) lives in
[docs/game-development-constitution/](docs/game-development-constitution/).

**Whenever the work touches a game design domain, consult it before proposing or
implementing the change.** That means any time the question is *what the game should do to
the player*, not just what the code should do — including:

- game feel, controls, responsiveness, camera, juice/polish (`FEEL`, `ANIM`)
- core loop, verbs, progression, difficulty, pacing (`DESIGN`, `SYS`, `BAL`, `PROTO`)
- levels, arena layout, encounter and space design (`LEVEL`, `CONTENT`)
- UI, HUD, menus, onboarding, accessibility (`UX`)
- economy, loot, rewards, monetisation (`ECON`, `MON`)
- narrative, quests, dialogue (`NARR`)
- audio design (`AUDIO`)
- system architecture, performance and tech-direction tradeoffs (`ARCH`, `PROG`, `PERF`, `TECH`)
- playtesting, QA, production and shipping process (`PLAYTEST`, `QA`, `PROD`, `SHIP`, `TEAM`, `VISION`)

Pure plumbing with no design content — fixing a compile error, wiring a prefab reference,
renaming a field — does not need it.

### How to use it

1. Open [docs/game-development-constitution/INDEX.md](docs/game-development-constitution/INDEX.md)
   and pick the **1–5 principles** that bear on the decision (or grep `principles/` for
   terms). Never read all 143.
2. Read each selected file **in full**. The statement alone is not enough — `Applies when`,
   `Does not apply / Exceptions`, `Disagreement`, `depends_on` and `conflicts_with` are
   where the conditions live.
3. Apply the principle's stated scope *before* recommending anything.
4. **Cite the principle IDs** (e.g. `GDC-L1-FEEL-0002`) in the explanation, so the
   reasoning is checkable.
5. Distinguish `objective` / `contextual` / `stylistic` guidance, and surface recorded
   disagreement rather than flattening it into false consensus.
6. It is **decision support, not an oracle**. Eclipse's own evidence — playtests, profiler numbers,
   what the team has already decided — overrides the corpus. When they conflict, say so
   explicitly instead of deferring to the principle.

Confidence is evidence strength (1–5), not certainty. Upstream and update instructions:
[docs/game-development-constitution/README.md](docs/game-development-constitution/README.md).
