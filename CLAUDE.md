# Eclipse

Unity 6 project (6000.3): a **single-player fighting game**. Carved out of the AIR codebase on
2026-09-18, which was itself carved out of SpaceGame. What was kept is the framework — the player
character and camera, health and damage, ragdolls, weapons and projectiles, the agent/AI stack,
inventory and hotbar, interaction, audio, the UI shell, input, spawning and scene management.

Everything domain-specific to AIR was removed: flight (ornithopter, wingsuit, wing pack), the
grappling hook, terrain and world streaming, the generated flying world, mounts and riding, the
racing and minigame modes, NPC caravans and the Ostrich, the backpack, save/load, and the whole
multiplayer stack.

**The game is single-player and has no notion of a second machine.** The last remnants of the
netcode shape — the `Network` authority shim, the `NetMessaging` / `NetChannel` / `NetMsg` message
bus, `AgentAuthority`, the `Cosmetic` projectile flag and the ragdoll's driver/watcher split — were
deleted on 2026-09-18. What was routed through a message id is a direct call now: a use goes
`OnRequestUse` → `PlayUse` → `TryUse` in `EquipmentController`, a knockdown is
`PlayerRagdoll.Knockdown` / `AgentRagdoll.Knockdown`, a blast on the player is `FlungBody.Fling`.
The describe/do/present split items and weapons are written in survives, because it is a gameplay
shape rather than a netcode one: `OnRequestUse` fills in a `UseContext`, `Use()` runs the effect
and `Present()` plays the look and sound.

Namespaces are still `SpaceGame.*` throughout. That is inherited, not a decision — renaming them
is a mechanical change nobody has made yet.

## What is here

| Area | Where | Entry points |
| --- | --- | --- |
| Player | `Scripts/Characters/Player/` | `PlayerController`, `Movement`, `PlayerLook`, `PlayerStance`, `PlayerView` |
| Combat | `Scripts/Weapons/`, `Scripts/Gameplay/Health/` | `Weapon`, `Projectile`, `HealthComponent`, `Damage` |
| Ragdolls | `Scripts/Gameplay/Ragdoll/` | `RagdollRig`, `PlayerRagdoll`, `AgentRagdoll`, `RagdollBudget` |
| Agents / AI | `Scripts/agents/` | `AgentController`, behaviour modules, `EnemyBrain`, `NpcBrain` |
| Locomotion | `Scripts/Locomotion/` | `LeggedLocomotion` and the walker rig/gait/IK stack |
| Items | `Scripts/Items/` | `InventoryItem`, `UsableItem`, `UseContext`, `EquipmentController`, `HotbarController` |
| Interaction | `Scripts/Gameplay/Interaction/` | `Interactor`, `IInteractable`, `InteractableTrigger` |
| Spawning | `Scripts/Gameplay/Game/Spawning/` | `SpawnManager`, `SpawnPoint`, `SpawnClearance` |
| Teleporting | `Scripts/Core/Motion/Teleport.cs`, `Scripts/Core/Teleporting/` | `Teleport.Move`, `ITeleportAware` |
| UI | `Scripts/Presentation/UI/` | `MainMenuUI`, `PauseMenuUI`, `LoadingScreenUI`, HUD, `GameplayMenuScope` |

Scenes: `Scenes/Core/Bootstrap.unity`, `Scenes/Core/MainMenu.unity` and the fight scene
`Scenes/Arena/Arena.unity` (inherited from AIR's minigame arena). Those three are what is in Build
Settings.

## State of the carve-out

Scripts compile and the EditMode suite passes. Scenes and prefabs have **not** been opened in the
Editor since the carve, so the ones inherited from AIR still carry components whose scripts are
gone — a `PlayerCharacter` with missing scripts, a HUD wired to screens that no longer exist.
Expect "missing script" warnings on first open, and clean them there rather than in the YAML.
(`SpawnManager.prefab` and `InventoryItemModule.prefab` had their netcode components stripped from
the YAML already, since those two were identifiable by name.)

The player prefab, `SpawnManager`'s prefab field and the main menu's scene reference are the three
things to re-wire before a fight can actually start.

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

`SpaceGame.Locomotion` and the pure gameplay rules are testable without the Editor, and the
EditMode suite is the cheapest verification this project has. A decision buried in an `Update()` is
a decision nothing can test.

## Skills

| Skill | Use it for |
| --- | --- |
| [spacegame-agent](.claude/skills/spacegame-agent/SKILL.md) | Creatures, NPCs, enemies: AI behaviour and factions |
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
