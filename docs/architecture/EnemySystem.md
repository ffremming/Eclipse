# Enemy System

How hostile NPCs work in Eclipse. Replaces the modular agent stack that was deleted on 2026-09-18
(`AgentSystem.md` and `EntitySystem.md` described that system and went with it).

## What it does

Goblins live around a camp. They stroll about it while nothing is happening, attack the moment they
see you, shout so the neighbours join in, and walk home if you run far enough.

## Why it is shaped like this

The system it replaced was 76 files and 12,604 lines: prioritised `IBehaviourModule` components
arbitrated by an `AgentController`, plus factions, relationship tables, a target registry, noise
emitters, cover points, formations and turrets. None of it was wired to a single prefab and no test
touched any of it.

That indirection is worth paying for when behaviours combine in ways a switch cannot express. These
enemies do four things in sequence, so they get a four-state machine and the modules are gone. The
trade is real and worth knowing: a genuinely different archetype now means editing `EnemyBrain`
rather than dropping in a module. For two or three archetypes that is cheaper. Past roughly six it
is not, and a seam should come back then.

## The pieces

| File | Kind | Job |
| --- | --- | --- |
| `Enemies/AI/EnemyBrain.cs` | plain C# | The state machine. All the decisions. |
| `Enemies/AI/EnemySenses.cs` | struct | Everything the brain is allowed to know, per tick. |
| `Enemies/AI/EnemyDecision.cs` | struct | What the brain decided. |
| `Enemies/AI/EnemySettings.cs` | struct | The tunables, passed in from the Inspector. |
| `Enemies/AI/EnemyState.cs` | enum | Wander, Chase, Attack, ReturnHome. |
| `Enemies/AI/Cone.cs` | static | Range-and-arc test, shared by sight and melee. |
| `Enemies/EnemyAgent.cs` | MonoBehaviour | Gathers senses, ticks the brain, drives the NavMeshAgent. |
| `Enemies/EnemyBase.cs` | MonoBehaviour | The camp: home point, wander radius, defended radius. |
| `Enemies/EnemyAlert.cs` | static | Registry of live enemies, and the shout that wakes them. |
| `Enemies/EnemyAnimator.cs` | MonoBehaviour | Drives the Goblin animator's parameters. |
| `Enemies/MeleeStrike.cs` | MonoBehaviour | What a swing hits. **Shared with the player.** |

`Enemies/AI/` is its own assembly (`SpaceGame.EnemyAI.asmdef`) for the reason
`SpaceGame.Locomotion` is: it has no gameplay dependencies, so the EditMode suite can reach it
without an Editor. The MonoBehaviours stay in `Assembly-CSharp` because they need `Damage`,
`HealthComponent` and `Sfx`.

## The loop

```
EnemyAgent.Update
  ├─ ResolveTarget()      find the "Player"-tagged object (re-resolved; the player respawns)
  ├─ UpdateSight()        Cone.Contains + one raycast, on an interval, not per frame
  ├─ ReadSenses()         → EnemySenses
  ├─ brain.Tick(senses)   → EnemyDecision          ← every decision happens here
  └─ Act(decision)        SetDestination / face / Swing() / Shout()
```

## Rules worth knowing

**Aggro is a deadline, not a flag.** Seeing you, hearing an ally, taking a hit, or you walking into
camp all push `aggroUntil` forward. Losing sight does not clear it, so breaking line of sight buys
a few seconds of being chased rather than an instant reset.

**A shout never echoes.** `ShoutAlert` is raised only when an enemy works it out for itself — saw
you, or was hit. An enemy that was *told* does not pass it on, so a camp cannot shout itself awake
in a loop.

**The leash drops aggro, it does not just redirect.** Past `LeashRadius` from camp the enemy
forgets you entirely, rather than walking home still angry and turning round on arrival.

**Wander has hysteresis.** It leaves Wander at the wander radius but only re-enters it at the
arrival radius. Matching the two would leave an enemy stopped on the line flipping state per tick.

**Pace is the brain's call.** `EnemySettings.WalkSpeed` applies while strolling or walking home and
`ChaseSpeed` while closing on the target; `EnemyAgent` copies the decision's `Speed` onto the
`NavMeshAgent` each tick. `ChaseSpeed` is also what the animator treats as full throttle, so it
should match what the fastest movement clip covers — otherwise the feet skate. Both default to the
goblin's 3.5, so an enemy that sets neither behaves as it always did.

**Melee is one implementation.** `PlayerMeleeSwing` decides *when* the player swings;
`MeleeStrike` decides *what a swing hits*, and the enemies drive the same component. `MeleeStrike`
uses timed windows rather than animation events because the goblin clips are shared between the
player and the enemies, so there is nowhere in the clip to put a windup that differs between them.

## Building an enemy

`Assets/Game/Prefabs/Enemies/GoblinEnemy.prefab` is the reference: NavMeshAgent, HealthComponent,
MeleeStrike, EnemyAnimator, EnemyAgent, a kinematic Rigidbody and a CapsuleCollider, with the
Goblin FBX and its controller as a child.

`GoblinCamp.prefab` is an `EnemyBase` with four of them parented to it, each with `home` pointed at
the camp. Drop it in a scene that has a baked NavMesh and it works.

### The Mountain Dragon

`Assets/Game/Prefabs/Enemies/MountainDragon.prefab` is the same stack at a very different scale, and
shows what actually has to change: the numbers, the collision shape and the animator controller —
no code. It is about 12 m long, so:

- **Three capsules, not one.** `MeleeStrike` asks whether a collider's bounds *centre* is inside the
  swing arc, so a single capsule along the body could only be hit near its middle. Torso, neck-and-head
  and tail base are separate colliders so a swing near any of them counts.
- **`StrikeOrigin`**, an empty 4.5 m ahead of the root, is the `MeleeStrike` origin. The root sits at
  the pelvis, and the head slam lands well in front of it; `attackRange` (6.5 m) is measured from the
  root and the strike sphere reaches past that.
- **The strike timing follows the clip.** The head comes down about a second into `Attack`, so
  `windup` is 1 s and `hitWindow` 0.35 s. Retime the two together if the clip changes.
- **`eyeHeight` is 4.5 m**, above its own colliders. Lower and the sight ray starts inside the torso
  and hits it, which reads as "blocked" and blinds the dragon. The same value is used for the height
  on the player that it looks at, so it aims over the player's head; fine for a dragon.
- **The controller** (`Art/Animations/Creatures/MountainDragon.controller`) declares the Goblin
  controller's parameters (`SpeedX`, `SpeedY`, `IsGrounded`, `AttackIndex`, `Attack`, `Hurt`, `Die`)
  so `EnemyAnimator` drives it unchanged. Locomotion is a 1-D blend on `SpeedY`: Idle at 0, Walk at
  0.4 (2 m/s of a 5 m/s `chaseSpeed`), Run at 1. `Hurt` is declared but has no state. It does not
  declare `MoveAnimSpeed`, the Goblin controller's walk-cycle rate, and `EnemyAnimator` skips writing
  it for a controller without it.

The model is the Sketchfab "Mountain Dragon" (CC-BY 4.0, Alexey Zaika), imported through
`MountainDragon.fbx`. It arrived as one 97-second take; the importer's clip list cuts it into
`Idle` (frames 1629–1841), `Walk` (56–343), `Run` (388–486), `Attack` (1252–1300) and `Die`
(2027–2115). The animation is a showcase reel with no walk-with-travel: every clip is in place, so
`applyRootMotion` is off and the `NavMeshAgent` moves it. There is no dedicated attack or death
clip — `Attack` is one head-slam lifted from a looping sequence of them, and `Die` is a collapse to
the ground from a longer crouch.

The model is authored in centimetres and imports at scale 1, so the `Model` child carries a 0.01
scale and an offset that puts the pelvis at the root and the soles on the ground plane.

## What is not done

- **The `Die` clip is a collapse, not a death.** It plays once and holds; the collider, the
  `HealthComponent` and the body stay in the scene.
- **The agent is on the humanoid NavMesh.** The bake uses agent type 0 (radius 0.5), so a dragon with
  a 1.6 m radius will clip corners and trees. A dedicated agent type would fix it; it needs its own
  bake.
- **No ragdoll on the enemy prefab.** `AgentRagdoll` and `RagdollRig` exist and `AgentRagdoll` has
  been repointed at `EnemyAgent`, but neither is on `GoblinEnemy.prefab` yet. Adding them is the
  obvious next step and `RagdollRig` builds itself from the skeleton.
- **No death handling beyond stopping.** `EnemyAgent` disables itself on death and the animator
  fires its `Die` trigger. Nothing despawns, loots or respawns.
- **Enemies block each other's line of sight**, because `sightBlockers` defaults to the Default
  layer, which is where their colliders are. Give enemies their own layer and exclude it if that
  turns out to matter. The *player* is already excluded — the raycast accepts a hit on the target's
  own body as "seen".
