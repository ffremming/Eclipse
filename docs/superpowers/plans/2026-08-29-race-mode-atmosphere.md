# Race mode menu + atmosphere (clouds, fog, sky, bigger world) — plan

Follows [2026-08-29-air-racing.md](2026-08-29-air-racing.md). Decisions, then steps.

## Decisions

| Area | Chosen | Why |
| --- | --- | --- |
| Race in the menu | Third front-menu mode **RACE** → Host / Join → lobby (`LobbyRoute.RaceHost/RaceJoin`) → world. Lobby data gains `LobbyMode {Story, Versus, Race}`; the race lobby seats `RaceRules.MaxRacers` (8). | Same shape as VS; joiners filter the browser by mode. No rules page — laps live on the director. |
| Race start in-world | Host sets `RaceSession.Begin()` at Start game; `RaceDirector` auto-starts the countdown once every connected client has a player and `autoStartDelay` has passed; after Results the pylon starts the next race. | No "all spawned" event exists; polling `PlayerIdentity.All` vs `ConnectedClientsIds` on the server is the honest signal. |
| Clouds | Two cheap layers, no raymarching: a **cloud deck** (huge disc at 480 m, scrolling FBM, sun-lit, near-camera fade) and **mesh cloud puffs** (clusters of low-poly spheres from `FlyingWorldLayout.CloudPlan`, drifting, soft fresnel shader). | The view is 4–10 km; volumetric raymarch ports (HDRP→URP) cost half-res + temporal passes for a stylised world that reads better flat-shaded. Sources in the spec. |
| Fog | Keep URP distance fog (exp², lower density) **plus** a `HeightFogFeature` full-screen pass (depth-reconstructed, exponential height falloff, fog colour = horizon colour) tunable from `WorldAtmosphere` in the scene. | Horizon haze that sits low over the sea and thins with altitude is what reads as "sky" from 200 m up; distance fog alone flattens everything. |
| Skybox | New `SpaceGame/AirSkybox` shader: zenith/horizon/nadir gradient, horizon haze band, sun disc + glow, high cirrus band. `AirSkybox.mat` created once by the builder, hand-tunable. | The desert sky's dust band and mountains are the wrong world. |
| Render distance / scene | Camera far clip 4000 → 10000; `WorldRadius` 620 → 1100 with more stacks and an outer ring of tall spires; seabed 4000 → 8000 m; water grid 16000 m; fog tuned to the new radius. | Longer sightlines need something to look at; the outer ring gives the horizon silhouettes. |

## Steps

1. Core: `LobbyMode`, `LobbyTeams.ModeOf`, `LobbyOptions.Create(mode)`, `LobbySession.CreateAsync(mode)`, `RaceRules`, `RaceSession`; `LobbyRoute` Race routes + `Accepts(LobbyMode)`; tests updated.
2. Menu: `MainMenuUI.StartRace/HostRace/JoinRace`; `LobbyUI` names/creates a race lobby; `RaceSession.Begin()` on start; a fourth `RACE` button in `MainMenu.unity` (editor script clones the VS entry).
3. `RaceDirector` auto-start when `RaceSession.IsActive`.
4. Shaders: `AirSkybox`, `CloudDeck`, `CloudPuff`, `HeightFog` + `HeightFogFeature` (added to `PC_Renderer`).
5. World: `FlyingWorldLayout` bigger + `CloudPlan`; `CloudMeshBuilder`; builder places deck, puffs, `WorldAtmosphere`, new skybox; camera prefabs far clip.
6. Rebuild world; compile; tests; console.

## Status
Compiled; world rebuilt (105 stacks, 48 clouds, AirSkybox, HeightFog pass on PC_Renderer); RACE entry verified in MainMenu.unity (ButtonRow, between VS and Quit, bound to StartRace); EditMode 1256 pass, 14 pre-existing failures (backpack/mount/Lasso, plus another session's wingsuit work).

Verified visually from editor captures: sky gradient + cirrus, cumulus puffs, ocean, and the bigger world all read correctly.

**Known issue — height fog seam.** The full-screen height fog shows a hard horizontal band at eye level and is stronger than its own maths predicts (measured against captures: nominal 0.0006 looked like ~70% extinction at 300 m where the model says ~8%). Density is tuned down to 0.00015, which looks acceptable, and the removable-singularity guard in `opticalDepth` was fixed, but the seam survives — so the suspect is now the world-position reconstruction in the full-screen pass (`ComputeWorldSpacePosition` with `UNITY_MATRIX_I_VP`), not the integral. Next step: output reconstructed distance as debug colour and compare with a known reference, or switch to reconstructing the view ray from camera frustum corners passed as globals. Unity distance fog + the skybox haze band carry the look meanwhile; setting `heightDensity` to 0 on `WorldAtmosphere` disables the pass entirely.
