# Air racing — implementation plan

Spec: [2026-08-29-air-racing-design.md](../specs/2026-08-29-air-racing-design.md).
No `dotnet` on this machine: compile and tests run through the open Editor
(`HeadlessTestRunner.RunEditModeDeferred`, results in `Temp/headless_tests.txt`).

## Phase 1 — Layout promotion
- [x] `Scripts/World/Flying/SpaceGame.World.Flying.asmdef` (no references).
- [x] Move `Editor/World/FlyingWorldLayout.cs` (+ `.meta`) there; namespace `SpaceGame.World.Flying`;
      add `FlyingWorldLayout.DefaultSeed = 7` (was `FlyingWorldBuilder.WorldSeed`).
- [x] `using SpaceGame.World.Flying;` in `FlyingWorldBuilder`, `FlyingWorldAssets`,
      `SeaStackMeshBuilder`, `FlyingWorldTests`. Tests asmdef references the new assembly.

## Phase 2 — Racing core (`Scripts/Gameplay/Racing/Core`, asmdef `SpaceGame.Racing.Core`)
- [x] `RingGate`, `RaceCourse`, `RaceCourseRules`, `RaceCourseGenerator`, `GateCrossing`,
      `RaceProgress`, `RaceStandings` (+ `RaceStandingEntry` plain), `RacePoints`, `Powerup` +
      `PowerupRoll`, `RaceTimeFormat`, `RacePhase`, `RaceEffectKind`.
- [x] Tests in `Tests/EditMode/`: `RaceCourseGeneratorTests`, `GateCrossingTests`,
      `RaceProgressTests`, `RaceStandingsTests`, `PowerupRollTests`.

## Phase 3 — Flight model
- [x] `OrnithopterFlightState.BoostThrust/BoostSecondsLeft`; `Step` consumes them.
- [x] `OrnithopterFlightModel.ApplyUplift/ApplyDisruption` (pure) + motor wrappers.
- [x] Three tests in a new `OrnithopterFlightEffectsTests`.

## Phase 4 — Runtime (`Scripts/Gameplay/Racing/`)
- [x] `NetMsg.RaceEffect = 90`, `RaceBomb = 91`; `SfxId` 1100 block (1000 is portals).
- [x] `RaceDirector` (`.cs`, `.Progress.cs`, `.Powerups.cs`, `.Records.cs`), `RaceStandingEntryNet`.
- [x] Player prefab: `RacerTracker`, `RacerEffects`, `RacerInput`.
- [x] World: `RaceCourseView` (+ `RaceRingMesh`), `RaceConsole`, `RaceRecordsSaveable`.

## Phase 5 — Presentation (`Presentation/UI/Racing/`, `Widgets/LeaderboardTable`)
- [x] Extract `LeaderboardTable` from `MatchLeaderboardUI`; `MatchLeaderboardUI` uses it.
- [x] `RaceLeaderboardUI`, `RaceHudUI`.

## Phase 6 — Editor wiring
- [x] Ring materials are runtime (`RaceMaterials`); `FlyingWorldAssets.EnsureRaceConsoleMaterial`; builder places `RaceDirector.prefab` + console.
- [x] `RaceDirector.prefab` created by `Tools ▸ SpaceGame ▸ Racing ▸ Build Race Prefabs`
      (also adds the three components to `PlayerCharacter.prefab`); registrar sync.
- [x] Rebuild world; run EditMode tests; console clean; autotest race step written.
- [ ] Two-process autotest build run (`-sgmode host` / `-sgmode client`) — not run this session.
- [ ] In-editor play-mode smoke test — blocked by an NGO host failure ("Allowed types … Count: 0") on the MCP play recipe.
