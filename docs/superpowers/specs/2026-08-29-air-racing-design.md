# Air racing — procedurally generated ring courses, timed, Mario-Kart style

**Date:** 2026-08-29
**Status:** designed autonomously from the one-line brief; assumptions are called out inline and
are the first things to revisit in review.
**Scope:** a race mode inside the existing FlyingWorld: a seeded ring course through the sky,
per-racer timing, checkpoints, a finish, a session leaderboard with placement points, and four
powerups (speed, bomb, protection, uplift). Multiplayer from the first line.

## The brief, and what it was read to mean

> a procedurally generated racing system, where players have to fly through rings in the air
> (stylised blue), and it is timed. supports multiplayer racing, mario kart style. points, timing,
> checkpoints, finish, leaderboard, powerups (speed/bomb/protection/uplift).

Assumptions taken (each is a one-line change if wrong):

| # | Assumption | Why |
| --- | --- | --- |
| A1 | The race runs **in the existing FlyingWorld**, started from a pylon on the spawn summit, not as a new front-menu mode. | Lowest friction to reach; the VS lobby route is a menu-only layer and a race needs the world anyway. A menu route can be added later on top of `RaceDirector`. |
| A2 | The course is a **closed loop**, one lap by default, laps tunable. | "Checkpoints + finish" fits a loop; a loop also means the finish is where everyone started. |
| A3 | **Each racer's clock starts when they cross the start gate**, not on a shared "go". | The ornithopter has no throttle and cannot hover at a start line; a shared go would penalise whoever launched a second late. Position (place) is still live-ranked by progress, so it still *feels* simultaneous. |
| A4 | Gate crossings are **detected by the owner and validated by the server** (must be the *next* gate; time is the server clock). | The player body and craft are owner-authoritative here (`FlungBody`, `NetAuthority`). The server never has a better position than the owner. Casual stakes → this is the exception `GDC-L1-MP-0004` permits; validation of *order* keeps the cheap cheats out. |
| A5 | One powerup held at a time; used with **F**. Pickups are boxes on the course. | Mario Kart's model. F is unbound while flying (W/S/A/D/Space/Ctrl/Esc are taken). |
| A6 | Points are per **session** (a cup tally across races), best times are per **world save**. | "Points" and "leaderboard" want a tally that outlives one race; best times are the thing worth persisting. |

## Design rationale (Game Development Constitution)

- **Counterplay, not parity** (`GDC-L1-BAL-0004`, `GDC-L1-DESIGN-0002`): the four powerups form
  a loop. *Bomb* punishes whoever is ahead and bunched; *Protection* eats exactly one bomb;
  *Speed* is raw pace, strongest when nobody has a bomb; *Uplift* trades nothing for the resource
  this flight model actually runs on — altitude/energy — so it is the leader's recovery and the
  trailer's shortcut. Distribution is placement-weighted (trailing racers roll bombs and uplift
  more often; the leader rolls protection), which is the Mario Kart rubber band. Weights are
  data in `PowerupRoll`, tuned by play, not by argument (`GDC-L1-BAL-0005`).
- **Legible space** (`GDC-L1-LEVEL-0002`): the course never fights the map — gates are placed
  *between* stacks with a margin larger than the tightest turning radius the layout file already
  derives (~45 m), the *next* gate is always drawn brighter than the rest, and an off-screen
  arrow points at it. Rings are one saturated blue on a warm-grey world: the eye finds them.
- **Feedback** (`GDC-L1-FEEL-0004`): a gate crossing gets a sound, a flash on the ring, and a
  HUD split; a pickup gets a sound and the slot filling; a bomb hit gets a shake, an FOV kick
  (the `FlungBody` recipe) and the craft visibly losing air. Nothing meaningless is juiced.
- **Sensation over accuracy** (`GDC-L1-FEEL-0007`): a boost is extra thrust *inside the flight
  model*, so it feels like the wings biting harder, not like a hand shoving the craft. Uplift
  pitches the flight path up and adds speed — a thermal, not a teleport.

## Architecture

```
Scripts/World/Flying/           SpaceGame.World.Flying   (new asmdef; FlyingWorldLayout moved here)
Scripts/Gameplay/Racing/Core/   SpaceGame.Racing.Core    (pure; refs World.Flying; EditMode-tested)
Scripts/Gameplay/Racing/        Assembly-CSharp          (NetworkBehaviour + MonoBehaviours)
Scripts/Presentation/UI/Racing/ Assembly-CSharp          (HUD, leaderboard)
Scripts/Vehicles/Ornithopter/   SpaceGame.Vehicles.Ornithopter  (boost/uplift in the flight model)
```

### Layout promotion (prerequisite)

`FlyingWorldLayout` (+ `StackPlan`, `BeamPlan`, `FlyingWorldPlan`) moves from
`Assets/Game/Editor/World/` to `Assets/Game/Scripts/World/Flying/` under a new runtime asmdef
`SpaceGame.World.Flying`, namespace `SpaceGame.World.Flying`. It is already a pure function of
the seed; only the folder was editor-only. The builder keeps calling `FlyingWorldLayout.Plan(7)`.
The race generator needs the stack footprints at runtime to thread gates between them, and this
is the only honest way to get them (an island manifest asset would duplicate what the seed
already encodes). `FlyingWorldBuilder.WorldSeed` moves with it as `FlyingWorldLayout.DefaultSeed`
so runtime and bake agree on which world they are talking about.

### Core (`SpaceGame.Racing.Core`) — pure, testable

| Type | Purpose |
| --- | --- |
| `RingGate` | `Center`, `Forward` (unit), `Radius`. A disc in space. |
| `RaceCourse` | `Seed`, `Laps`, `Gates[]` (gate 0 = start/finish), `PickupSpots[]` (positions). Immutable. |
| `RaceCourseRules` | Every generation constant (gate count, radius, spacing band, altitude band, climb/descent per gate, clearance margin, max turn, pickup cadence) with the flight-derived reasoning beside each. |
| `RaceCourseGenerator.Generate(seed, plan, rules)` | Rejection-sampled loop: start over the spawn summit facing the arch, then successive gates within the spacing band, turning ≤ `MaxTurn`, descending net (climbs allowed, capped), clear of every stack at the gate's altitude and along the segment; the last gate turns back to close the loop. Deterministic per seed. |
| `GateCrossing.Crosses(from, to, gate)` | Segment/disc test in the gate plane, forward-only. |
| `RaceProgress` | Per-racer struct: `NextGate`, `Lap`, `StartMs`, `FinishMs`, `Finished`. `Advance(course, nowMs)` returns what happened (`Started`, `Checkpoint`, `LapCompleted`, `Finished`). |
| `RaceStandingEntry` | `ClientId`, `Name`, `Lap`, `NextGate`, `DistanceToNext`, `ElapsedMs`, `BestMs`, `Points`, `Held` (powerup), `Shielded`. `INetworkSerializable` lives on a Runtime mirror, not here. |
| `RaceStandings.Sort` | Finished by time, then progress (lap, gate, distance-to-next). Stable by name. |
| `RacePoints.ForPlace(place)` | `{15,12,10,8,7,6,5,4,3,2,1}`, 0 beyond. |
| `PowerupRoll.Roll(rng, place, racers)` | Placement-fraction weighted table over `Powerup {Speed, Bomb, Protection, Uplift}`. |
| `RaceTimeFormat.Format(ms)` | `m:ss.fff`. |
| `RacePhase` | `Idle, Countdown, Running, Results`. |

### Flight model additions (`SpaceGame.Vehicles.Ornithopter`)

- `OrnithopterFlightState.BoostThrust` (m/s², decays to 0 over `BoostSecondsLeft`, both fields on
  the state). `Step` adds `BoostThrust` to `thrust` and counts `BoostSecondsLeft` down. Boost is
  therefore stamina-free thrust — pace without the rhythm cost.
- `OrnithopterFlightModel.ApplyUplift(state, cfg, climbDegrees, airspeedGain)` — sets `Gamma` to
  at least `climbDegrees`, adds airspeed. Pure.
- `OrnithopterFlightModel.ApplyDisruption(state, airspeedFraction)` — the bomb: airspeed × fraction,
  `Stalled = true`. Pure.
- `OrnithopterFlightMotor.ApplyBoost(thrust, seconds)`, `ApplyUplift(...)`, `ApplyDisruption(...)`
  write those onto the live state on the simulating machine only.

### Runtime (`Assembly-CSharp`, namespace `SpaceGame.Gameplay.Racing`)

**`RaceDirector : NetworkBehaviour`** — one per world, in `Prefabs/Systems/RaceDirector.prefab`,
placed by the builder. Server-authoritative in the `MatchManager` shape, but split by concern:

- `RaceDirector.cs` — phase, seed, laps, timing. `NetworkVariable<int> phase/seed/laps`,
  `NetworkVariable<double> phaseStartedAt` (server time). Statics for UI: `Course`, `Phase`,
  `Standings`, `OnStandingsChanged`, `OnPhaseChanged`, `LocalHeld`. `RequestStartRpc(seed)` from
  any client (casual). Countdown → Running; Results when everyone has finished or
  `finishGraceSeconds` after the first finish; Results → Idle after `resultsSeconds`.
- `RaceDirector.Progress.cs` — `ReportGateRpc(gateIndex)`: accept only `gateIndex ==
  progress.NextGate`; stamp server time; recompute standings; broadcast table (`SendTo.NotServer`,
  the `MatchScoreEntry` decision: whole table, rarely). Awards points on finish.
- `RaceDirector.Powerups.cs` — pickup spot availability (server list, respawn after
  `pickupRespawnSeconds`), `ReportPickupRpc(spot)`, `UsePowerupRpc()`. Effects go out as
  `NetMsg.RaceEffect` on the *target player's* relay (A = effect, B = param) — the `Flung`
  recipe: broadcast, exactly one machine owns the body and acts, everyone presents. Bombs:
  server records position, after `bombFuseSeconds` scans `PlayerIdentity.All` within
  `bombRadius`; shielded targets lose the shield instead. `NetMsg.RaceBomb` (A = 0 placed / 1
  burst, P = position) on the director's own relay for VFX.
- `RaceDirector.Records.cs` — best time per seed, written on finish; hands the table to
  `RaceRecordsSaveable`.

**On the player prefab:**

- `RacerTracker` — owner-only sampler. Position = mounted craft when mounted, else the body.
  Each `FixedUpdate` tests `GateCrossing` against the next gate and pickup spots against a sphere;
  reports over RPC. Keeps a client-side echo of `NextGate` so it never double-reports.
- `RacerEffects` — `NetOn(NetMsg.RaceEffect)`. Owner: finds the mounted `OrnithopterFlightMotor`
  and applies boost / uplift / disruption; on foot, disruption is a `FlungBody`-style shove.
  Everyone: SFX + the shield visual.
- `RacerInput` — owner-only `Keyboard.current.fKey` → `UsePowerupRpc`. Same precedent as
  `SteerModule`'s Escape read.

**In the world:**

- `RaceCourseView` — builds the rings on every peer from `RaceDirector.Course` (not networked
  objects: the seed is the replication, exactly as the net-gun arc). Torus mesh generated once,
  `RaceRing.mat` (URP/Unlit, saturated blue, emissive) created by `FlyingWorldAssets` under the
  same never-overwrite contract. Next gate for the local racer is brighter; passed gates dim.
  Pickup boxes are slowly rotating cubes in the same blue, hidden while taken.
- `RaceConsole : IInteractable` — the pylon on the spawn summit. `Interact` → `RequestStartRpc`.
- `RaceRecordsSaveable` — global saver, key `raceRecords`, `{ seed: { ms, name } }`.

### Presentation (`Presentation/UI/Racing/`)

- `RaceHudUI` — built at runtime like the match UI. Countdown numerals; then place `2/4`, timer,
  `gate 5/12`, held powerup label, next-gate marker (ring on screen, arrow at the edge — via
  `WorldOverlay.Project`). Shows only while `Phase != Idle`.
- `RaceLeaderboardUI` — Tab-held / pinned in Results. Columns: place, name, time, best, points.
  The row/column/pin machinery is **extracted** from `MatchLeaderboardUI` into
  `Widgets/LeaderboardTable` and both boards use it (the copy-paste rule).

### Messages

`NetMsg.RaceEffect = 90` (server → all, on the target player's relay; A = `RaceEffectKind`, B =
param ×100), `NetMsg.RaceBomb = 91` (server → all, on the director's relay; A = 0/1, P = position).
RPCs on `RaceDirector` for everything else. New `SfxId` block 900–906.

## Course generation, concretely

Constants live in `RaceCourseRules`; defaults derived from `OrnithopterFlightConfig` numbers the
layout file already cites (stall 11 m/s, glide 9:1, turn radius 15–50 m):

- 12 gates, radius 9 m (a 20 m wingspan machine at 20 m/s needs a target it can hit at 60° bank).
- Spacing 90–150 m; max heading change per gate 65°.
- Altitude band 45–210 m. Net descent per gate −35..+12 m: a climb is possible with flapping;
  the loop closes by returning to the summit height only because gate 0 sits there — the last
  segment is allowed a climb up to `FinalClimbMax` (40 m); a glide at 9:1 over 150 m loses ~17 m,
  so the racer must flap somewhere. That is the skill.
- Clearance: gate centre ≥ stack radius-at-altitude + 25 m from every stack; the segment to the
  next gate sampled every 10 m against the same test.
- Pickup spot at the midpoint of every third segment.
- 400 rejection attempts per gate, then the generator backs up one gate; a seed that cannot
  close in 4 000 attempts total throws — a test asserts seeds 0–199 all generate.

## Multiplayer (non-negotiable 1)

- Authority: phase/seed/timing/standings/pickup availability/effect decisions on the server;
  positions and crossings sampled by the owner; effects applied by the owner (the only machine
  that simulates that craft); cosmetics everywhere.
- Rings and boxes are derived, not spawned — nothing new in `DefaultNetworkPrefabs` except
  `RaceDirector.prefab` (registrar sync).
- Verification: the autotest harness gets a race step — host starts a race, both peers report
  the course seed, the client crosses gate 0 (teleported through it) and the host's standings
  show the client started. Plus a manual two-process pass.

## Persistence (non-negotiable 2)

- Best times per seed persist via `RaceRecordsSaveable` (global saver). Verified by
  save → reload → the record is in the JSON and shows on the board.
- A race in progress, standings, held powerups: **transient by design** — a save mid-race
  reloads to `Idle`. The prefab-placed director holds no saveable state of its own.

## Testing (Core, ~3 per unit)

- Generator: deterministic per seed; 200 seeds all close; every gate clears every stack; spacing
  and turn constraints hold.
- `GateCrossing`: through the disc forward counts; backward doesn't; past the rim doesn't.
- `RaceProgress`: start → checkpoints in order → lap → finish; out-of-order gate ignored.
- `RaceStandings`: finished beats unfinished; more progress beats less; tie by distance.
- `PowerupRoll`: last place never rolls protection more often than first; distribution sums.
- Flight model: boost raises airspeed vs. baseline; uplift climbs; disruption stalls and slows.

## Out of scope (this spec)

Ghosts, spectating, a menu route for races, bots, split times per gate on the HUD, controller
bindings for F, and any art beyond the generated torus/box and the blue material.
