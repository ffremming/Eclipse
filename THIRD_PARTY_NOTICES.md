# Third-party notices

Assets in this repository that were not made here, and what each one's licence asks of us.
Keep this file current: a licence with an attribution clause is a promise the shipped game
has to keep, and the credits block below is how it keeps it.

Provenance was traced from the files themselves — the glTF `asset.extras` block Sketchfab
writes into a `.glb`, the FBX `ApplicationVendor` metadata, the original download archives
kept in `../Eclipse-asset-downloads/`, and the build records under
`Assets/Game/Art/Models/_Source~/`. Where the files carry no source, this file says so
rather than guessing.

**There is no credits screen in the game (2026-09-20).** Until there is one, the credits
block below has to appear on the itch.io page — see
[docs/itch-page.md](docs/itch-page.md) — and this file ships with any build.

## The credits block

Paste-ready. The three CC-BY lines are required; the rest are courtesy.

> **Models**
> "Mountain Dragon" by Alexey Zaika — CC BY 4.0
> "Torch" by milacetious — CC BY 4.0
>
> **Animation** — Adobe Mixamo; "Human Animations" by Kevin Iglesias
> **Vegetation** — "Low Poly Wind" by Nicrom
> **Camera shake** — Smooth Camera Shaker by First Gear Games
> Built with Unity.

---

## CC BY 4.0 — attribution required

These three are the only assets in the project with a licence that obliges us to name
anyone. All three came from Sketchfab, and each carries its own licence statement inside
the downloaded `.glb` (`asset.extras`), which is the record this section is taken from.

### Mountain Dragon

**What:** [`Assets/Game/Art/Models/Creatures/MountainDragon/`](Assets/Game/Art/Models/Creatures/MountainDragon/) —
`MountainDragon.fbx`, its five texture maps, and the clips cut out of the one 97-second take
it shipped with (`Idle`, `Walk`, `Run`, `Attack`, `Die`). Dressed by
`Assets/Game/Art/Materials/Characters/MountainDragon.mat` and driven by
`Art/Animations/Creatures/MountainDragon.controller`. **It is in the shipped game:** the
dragon stands in `Scenes/Core/MainMenu.unity`, behind the main menu.

**Author:** Alexey Zaika — <https://sketchfab.com/lexazaixa25>

**From:** "Mountain Dragon",
<https://sketchfab.com/3d-models/mountain-dragon-0a0665396f634b0e9d0872720087f52c>

**Licence:** CC BY 4.0, <http://creativecommons.org/licenses/by/4.0/>

**What the licence asks of us:** name the author, link the licence, and say that the model
was changed. It was: re-exported through Blender by
`Art/Models/_Source~/models/creatures/creature_import.py`, scaled from centimetres,
stripped of a stray `Icosphere`, its material rebuilt from the shipped maps, and its single
take cut into five clips. See [docs/architecture/EnemySystem.md](docs/architecture/EnemySystem.md)
for the detail.

### Torch

**What:** [`Assets/Game/Art/Models/Items/Torch/torch.glb`](Assets/Game/Art/Models/Items/Torch/torch.glb),
the torch item's model, textures embedded in the file.

**Author:** milacetious — <https://sketchfab.com/milacetious>

**From:** "Torch", <https://sketchfab.com/3d-models/torch-e34b86256f1f44fba4073fc0ee00fdd7>

**Licence:** CC BY 4.0, <http://creativecommons.org/licenses/by/4.0/>

**What the licence asks of us:** the same three things. The `.glb` is used as downloaded;
only Unity's import settings and the material it is given in the project differ.

### Supernova remnant : lighting 2 — downloaded, not shipped

**What:** `supernova_remnant__lighting_2.glb`, by Aiekick
(<https://sketchfab.com/Aiekick>), CC BY 4.0,
<https://sketchfab.com/3d-models/supernova-remnant-lighting-2-68cbcb486c7f41f2b842a36a543a54a1>.
It sits in the downloads folder and **is not in this repository or in any build**. Listed
here so that if it ever is imported, the attribution comes with it.

---

## Animation

### Adobe Mixamo

**What:** every clip under
[`Assets/Game/Art/Animations/Player/`](Assets/Game/Art/Animations/Player/) and most of
[`Assets/Game/Art/Animations/Creature/`](Assets/Game/Art/Animations/Creature/) — the walk,
run, strafe, crouch, jump, dodge, idle, stagger and melee-swing clips, and the `X Bot` rig
they were downloaded on. The FBX metadata names `Mixamo, Inc.` / `mixamo.com` as the
exporting application; the downloads they came from ("Pro Melee Axe Pack",
"Magic Spell Pack", and the loose clips beside them) are Mixamo packs.

**Licence:** Mixamo's own terms, which come with an Adobe account: the clips may be used in
a commercial project royalty-free, and **no attribution is required**. They may not be
redistributed as animation assets — shipping them baked into a game is exactly what they
are for.

**Unresolved:** which Adobe account they were downloaded under. Mixamo's licence is granted
to the account holder, so that account has to be the project's.

### Kevin Iglesias — Human Animations

**What:** [`Assets/ThirdParty/Kevin Iglesias/Human Animations/`](Assets/ThirdParty/Kevin%20Iglesias/Human%20Animations/).
Four of its clips are referenced by `Art/Animations/Creature/Creature.controller` and so are
in the shipped game: `Death`, `HumanM@Gun_Aim01`, `HumanM@Gun_Aim02` and `AssultRifleIdle`.
The rest (`Damage`, `assultRifleShooting`, the boomerang and spear throws) are imported but
unused.

**Licence:** the Unity Asset Store EULA — usable in a shipped game under the Standard
Unity Asset Store licence, not redistributable as source assets. No attribution is required;
the credit above is courtesy.

**Unresolved:** the pack's version and the Asset Store link are not recorded anywhere in the
repository, and the pack ships no readme. Fill both in from the purchase history before
release.

---

## World and rendering

### BugWarNature — including Nicrom "Low Poly Wind"

**What:** [`Assets/ThirdParty/BugWarNature/`](Assets/ThirdParty/BugWarNature/). Two things
in one folder:

- `BugWar/` — the trees, stumps, rock formations, ferns, mushrooms and ground cover the
  world is planted with, plus the `BugWar/GrassWind` and `BugWar/TreeBillboard` shaders. The
  glow mushrooms are these mushrooms grown 3–8×
  ([docs/architecture/GlowMushroom.md](docs/architecture/GlowMushroom.md)).
- `Nicrom/` — the vendor **Low Poly Wind** grass, flower and rock prefabs, and the
  `LPW_Vegetation` material the BugWar shaders copy their wind properties from (see the
  comment at the top of `BugWar/Art/Shaders/GrassWind.shader`).

**Licence:** Nicrom's Low Poly Wind is a Unity Asset Store package, used under the Standard
Unity Asset Store licence. No attribution is required.

**Unresolved:** the `BugWar/` half carries no licence text and no origin. It is not a
vendor pack layout — it looks like art carried in from another project. Establish who owns
it and under what terms before shipping, because it is most of what the world is made of.

### First Gear Games — Smooth Camera Shaker

**What:** [`Assets/ThirdParty/FirstGearGames/`](Assets/ThirdParty/FirstGearGames/), used by
`DamageFeedback`, `FlungBody`, `ScriptableObjects/Shake/DamageShake.asset` and the
`3rd person` camera prefab — so it is in the shipped game.

**Licence:** Unity Asset Store package under the Standard Unity Asset Store licence. No
attribution required.

### TextMesh Pro

**What:** [`Assets/ThirdParty/TextMesh Pro/`](Assets/ThirdParty/TextMesh%20Pro/), Unity's own
package essentials. It brings two notices of its own, both already in the folder:

- **Liberation Sans** — SIL Open Font License, text in
  [`Fonts/LiberationSans - OFL.txt`](Assets/ThirdParty/TextMesh%20Pro/Fonts/LiberationSans%20-%20OFL.txt).
- **EmojiOne** — attribution text in
  [`Sprites/EmojiOne Attribution.txt`](Assets/ThirdParty/TextMesh%20Pro/Sprites/EmojiOne%20Attribution.txt).

Keep both files with the assets; that is all either licence asks.

### Unity

The engine (Unity 6, 6000.3.11f1) and its packages — URP, the Input System, AI Navigation,
ProBuilder, Timeline, the Test Framework and the rest of `Packages/manifest.json` — under the
Unity Companion License and the Unity terms of service. Unity asks for no credit beyond the
splash screen; the "Built with Unity" line above is courtesy.

---

## Models with no recorded licence — resolve before release

These came from free-model download sites. **None of them shipped with a licence file**, and
the sites they came from were not written down at the time. Each entry gives the evidence
that is actually in the files, which should be enough to find the listing again. Until each
one is traced, the safe assumptions are: it may carry an attribution clause, and it may
forbid commercial use.

| Asset in the project | Evidence of origin | Download kept as |
| --- | --- | --- |
| [`Weapons/Sword/sword.fbx`](Assets/Game/Art/Models/Weapons/Sword/sword.fbx) + `0001–0004.png` | Exported from Blender 2.69 on 2014-08-02; the archive also holds `.3ds`, `.obj`, `.mtl` and a `sw_sword.jpg` listing image. The `tcnd28g8qx34-` prefix is a free3d.com download id. | `tcnd28g8qx34-Sword.zip` |
| [`Weapons/Khopesh/khopesh.fbx`](Assets/Game/Art/Models/Weapons/Khopesh/khopesh.fbx) + 11 TGA maps | Archive holds `EgyptKhopesh.blend` / `.fbx` / `.mtl` (2021-01-31) and a `maps_tga/` set dated 2017-10-23. This is the crumpy's weapon and the player's light khopesh. | `48-khopesh.zip` |
| [`Weapons/Axe/axe.obj`](Assets/Game/Art/Models/Weapons/Axe/axe.obj) + `axe_albedo.png` | `# Blender v2.79 (sub 0) OBJ File: 'untitled.blend'`; archive holds `Axe.blend`, `Axe.png`, `untitled.3ds/.mtl/.obj`. The alien's weapon. | `27-new-folder.rar` |
| [`Weapons/ChainWhip/chain_whip.obj`](Assets/Game/Art/Models/Weapons/ChainWhip/chain_whip.obj) | `# 3ds Max Wavefront OBJ Exporter v0.97b`, created 2012-10-18; the archive's mesh is named `17455_Chain_whip_V1.obj` and ships a blank `.mtl` and a blank image. | `Chain_whip_V1_L1.123cef96a6d2-…​.zip` |
| **The human base mesh** — and therefore the **Human**, **Alien** and **Crumpy** bodies sculpted from it | `FinalBaseMesh.obj` dated 2014-08-21, 24,461 quads, no UVs, no `.mtl`. The `fdx54mtvuz28-` prefix is a free3d.com download id. Both the archive and the imported OBJ were removed from the repository on 2026-09-20 and kept outside it; no copy of the original mesh is distributed any more. | `../Eclipse-asset-downloads/fdx54mtvuz28-FinalBaseMesh.rar` |

The last row is the one that matters most: every character in the game is a derivative of
that mesh (see
[`human_sculpt_base_BUILD.md`](Assets/Game/Art/Models/_Source~/models/characters/human_sculpt_base/human_sculpt_base_BUILD.md)),
so whatever it turns out to carry, the player and both enemies carry too.

**The goblin** was cut from the project on 2026-09-19. Its download,
`Goblin_FBX.rar`, is of the same unrecorded kind; nothing derived from it remains in the
repository.

---

## Audio — unresolved

The project had no audio at all until the music work now in progress
(`Assets/Game/Art/Audio/Music/`, `Assets/Game/Data/MusicLibrary.asset`). **Nothing is
recorded about where those fifteen tracks came from**, and two loose `.m4a` files sit in
the repository root under an artist-title filename with no source either.

No build should ship until every track has a line here: who made it, where it came from and
what it is licensed under. Music is the licence category most likely to carry a real
obligation, and a store page is publication.

---

## What is ours

For completeness, so nothing in this list is mistaken for a third-party asset. Everything
built in Blender from the scripted library under `Assets/Game/Art/Models/_Source~/` is this
project's own work: the **castle** and keep, the **lantern**, the **tower and wall keys**,
the **boomerang**, the **walking staff**, and the sculpts of the **Human**, **Alien** and
**Crumpy** — those three subject to the base-mesh question above. So are all of the shaders
under `Assets/Game/Art/Shaders/`, the terrain and its layers, every script under
`Assets/Game/Scripts/` and `Assets/Game/Editor/`, and the UI, which is built from code.
