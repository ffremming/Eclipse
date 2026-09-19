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
| Light (the player's health) | `Scripts/Gameplay/Light/`, `Scripts/Presentation/UI/Lantern/` | `LightCost`, `LightOrb`, `LightDropper`, `LanternHud` |
| Enemies / AI | `Scripts/Enemies/` | `EnemyAgent`, `EnemyBrain`, `EnemyBase`, `EnemyAlert`, `EnemyGear` |
| Dark (the enemies' light) | `Scripts/Gameplay/Dark/`, `Art/Shaders/Dark/` | `Unlight`, `DarkWeapon`, `DarkSwingTrail`, `DarkSlash` |
| World | `Scripts/World/` | `VegetationField`, `VegetationLayout`, `WorldAtmosphere` |
| Items | `Scripts/Items/` | `InventoryItem`, `UsableItem`, `UseContext`, `EquipmentController`, `HotbarController` |
| Interaction | `Scripts/Gameplay/Interaction/` | `Interactor`, `IInteractable`, `InteractableTrigger` |
| Spawning | `Scripts/Gameplay/Game/Spawning/` | `SpawnManager`, `SpawnPoint`, `SpawnClearance` |
| Teleporting | `Scripts/Core/Motion/Teleport.cs`, `Scripts/Core/Teleporting/` | `Teleport.Move`, `ITeleportAware` |
| Menu | `Scripts/Presentation/UI/` | `MainMenuUI`, `CursorSpotlight`, `RevealField` |
| Weapon wheel | `Scripts/Items/Inventory/Wheel/`, `Scripts/Presentation/UI/WeaponWheel/` | `WeaponWheel`, `RadialSelection`, `WeaponWheelView` |
| Glow mushrooms | `Scripts/Gameplay/Light/`, `Editor/AssetPipeline/GlowMushroomBuilder.cs` | `OrbBurstOnDeath`, `OrbBurst` — see [docs/architecture/GlowMushroom.md](docs/architecture/GlowMushroom.md) |

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

**UI is the main menu, the weapon wheel and the lantern, and nothing else.** There is no other HUD: no
crosshair, no death screen, no interaction prompt, no damage numbers, no pause menu. The lantern is
the player's health bar, drawn bottom right — see "Light" below. The wheel is
held on Q: the game slows, the look input steers a pointer round the dial instead of the camera, and
letting go equips the hotbar slot under it. It is built from code at runtime by `WeaponWheel`, which
sits on the player prefab, so there is no UI prefab or scene to keep in step with it. Q used to be
the flight-era Deploy action; nothing consumed it.

**Light is the player's health.** The player's `HealthComponent` holds 30 points, one per orb of
light, and its `lightCost` field makes a blow take the orbs it is worth instead of its damage:
`LightCost` (`Data/LightCost.asset`) maps damage to one orb under 20, two from 20, three from 30.
Events from a light-measured `HealthComponent` still report the raw damage, not the orbs.
`LanternHud` reads the player's health each frame and draws it as the lantern; like the wheel it is
built from code and sits on the player prefab. Light at zero is death, as health at zero always was.

**Light only ever flows one way.** `LightDropper` sits on the enemies and on nothing else: an enemy
that is hurt throws as many `LightOrb`s as the blow was worth onto the ground, where they hang
bobbing until the player walks into one and takes back a point. The player has no dropper, so light
knocked out of the lantern is gone at once rather than lying on the floor to be walked back over.
Against that, **every swing costs one light** — `PlayerMeleeSwing.swingLightCost`, paid through
`HealthComponent.Spend`, which is points rather than damage and is silent, so it drops no orbs and
flashes nothing. The last orb is never spendable, so a player can never swing their own lantern out.
The sources are enemies and glow mushrooms; the sinks are being hit and swinging. Balance to watch:
a bare fist does 12, which an enemy sheds one orb for, so swing-for-hit is break-even and every miss
is a point down.

The player's animation clips under `Art/Animations/Player/` were kept deliberately — they are
reusable — even though the body they were imported for is gone.

## State of the project

Scripts compile clean with no reference to a deleted type, no prefab or scene holds a dangling
reference, and there are no orphan `.meta` files. `PlayerCharacter.prefab` is tagged `Player` and
carries `PlayerController`, `Movement`, `PlayerLook`, `PlayerStance`, `PlayerMeleeSwing`,
`ThirdPersonCameraBoom`, `HealthComponent`, `Interactor` and `EquipmentController`.

The player wears the Human (`Art/Models/Characters/Human/Human.fbx`), a Humanoid rig built from the
sculpt in `Art/Models/_Source~/models/characters/human_sculpt_base/`. It runs on the shared clip
library, `Art/Animations/Creature/Creature.controller`, retargeted through the Humanoid avatar. So
do the two other creatures cut from the same sculpt — the **Alien** and the **Crumpy** — which are
rigged with the same skeleton and exported by the same `sculpt_character_export.py`. The EditMode
suite is green (178 tests).

### The alien and the crumpy

**The goblin is gone.** It was the only enemy, and these two replace it everywhere it stood: the
castle garrisons, the loose camp in the world, its prefabs, its model and its materials. What
survives it is the part that was never goblin-specific — the clip library it was animated with
(renamed `Art/Animations/Creature/`), and its body, saved off as `EnemyChassis.prefab`: collider,
rigidbody, agent, health, strike, animator driver and light dropper, with no species on it.

The two are built by `Editor/AssetPipeline/SculptEnemyBuilder.cs` from that chassis — model dropped
in, numbers retuned — so "every enemy fights the same way" cannot drift. What they are is one table, `SculptEnemy.cs`: the crumpy is the common one (65 health, quick
hooked khopesh, long arms), the alien the rare one (190 health, slow axe, sees furthest). Their
camps are `EnemyCampPlacement.cs` — thirteen crumpies in three camps, three aliens in two — and
`EnemyCampBuilder` stands them up, which the world build calls and
**Tools > Eclipse > World > Place Enemy Camps** re-runs on a scene that already exists.

**Their weapons emit darkness**, which is the light system read backwards. `Unlight` is a Light
whose colour is negative, so URP's additive light loop *subtracts* it from whatever is nearby —
Unity clamps a negative `intensity` to zero, but a negative colour survives all the way through.
`DarkWeapon` drives it off the creature's own `MeleeStrike`, through the same `SwingFlare` curve the
player's `LightWeapon` swings on.

The blade also **drags a streak of pitch black behind it**, which is the player's slash of light
read backwards and is mechanically the same thing: `SwingRibbonTrail` records a ribbon between a
hilt anchor and a tip anchor while they are moving fast, `SlashRibbon` builds the mesh and
`SweepDetector` decides what counts as a swing. `SwingTrail` is the player's half of it and
`DarkSwingTrail` the creature's — the only differences are who announces the swing
(`PlayerMeleeSwing` against `MeleeStrike`) and what it is painted with. `DarkSlash.shader` is
`LightSlash` term for term, multiplied into the frame instead of added to it and multiplied straight
to `DARK_COLOUR_VOID` from `DarkPalette.hlsl` — the palette's own pitch black, skipping its violet
fringe and body, because a swing should read as the world being taken away rather than as a coloured
substance in the air. The consequence to keep in mind: black on black cannot be seen, so a creature's
swing reads exactly as far as the player's own light reaches.

`DarkArc` and `DarkHalo`, the multiply-blended twins of `LightArc` and the tip glow, were deleted on
2026-09-19 along with the halo sphere on the blade. They never animated: `EnemyGear` spawned the
weapon parentless and seated it in the hand afterwards, so `DarkWeapon`'s `GetComponentInParent`
found no `MeleeStrike` at `Awake` and subscribed to nothing. The seating now spawns the prop into
the hand, the way `EquipItemSocket.Equip` always has.

Enemies reach the world three ways, all from the same roster shape (`CampRoster`): the camps
`EnemyCampBuilder` places, the castle garrisons `CastleBuilder` places (mixed the same way — mostly
crumpies, a few aliens), and `DarkCamp.prefab`, a camp of four crumpies and two aliens round one
`EnemyBase` that you drag in by hand, as `GoblinCamp.prefab` used to be. Rebuild it from
**Tools > Eclipse > Enemies > Build Dark Camp Prefab**.

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
