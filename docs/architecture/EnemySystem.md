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

## What is not done

- **No ragdoll on the enemy prefab.** `AgentRagdoll` and `RagdollRig` exist and `AgentRagdoll` has
  been repointed at `EnemyAgent`, but neither is on `GoblinEnemy.prefab` yet. Adding them is the
  obvious next step and `RagdollRig` builds itself from the skeleton.
- **No death handling beyond stopping.** `EnemyAgent` disables itself on death and the animator
  fires its `Die` trigger. Nothing despawns, loots or respawns.
- **Enemies block each other's line of sight**, because `sightBlockers` defaults to the Default
  layer, which is where their colliders are. Give enemies their own layer and exclude it if that
  turns out to matter. The *player* is already excluded — the raycast accepts a hit on the target's
  own body as "seen".
