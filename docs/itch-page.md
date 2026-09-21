# itch.io page copy

Paste-ready text for the Eclipse store page. Written for whoever fills in the itch.io form — the
headings match the fields on it. Everything here is true of the build as it stands; the two
"still needed" notes at the bottom are the parts that cannot be written, only made.

---

## Title

Eclipse

## Short description (the one-liner under the title)

A forest under a dead sun. Your lantern is your health, every swing spends it, and the only light
left is the light you take back off what you kill.

## Classification / details

| Field | Value |
| --- | --- |
| Kind of project | Game |
| Release status | Prototype |
| Platform | Windows / macOS (whichever you upload; tick only what you actually test) |
| Pricing | Free / name your own price |
| Genre | Action |
| Tags | third-person, melee, dark, atmospheric, singleplayer, unity, low-poly, exploration |
| Input | Keyboard & mouse |
| Average session | A few minutes |
| Accessibility | No audio at all — the game is fully playable silent, because it is silent |

## Description (the body of the page)

**There is no health bar.** You carry a lantern of thirty points of light, drawn bottom right, and
every point is something you can lose or spend.

- Being hit takes light — one orb under a light blow, three under a heavy one.
- **Every swing costs light too**, so a miss is a point down and a landed hit roughly breaks even.
- Hurting something knocks light loose. It falls on the ground and hangs there bobbing until you
  walk over it and take it back.
- Light at zero is death — and death is a third of a lantern and the walk back, not the end of the
  run. Everything you cleared stays cleared, every key you found is still on you.

The dark is the real opponent. The further you push the less you can see, and standing still to
recover is not an option, because the light on the floor is the light you just lost.

**Glow mushrooms are the other supply.** They burst into orbs when they are broken, so the bright
patches on the forest floor are the ones worth walking through.

**The creatures carry darkness**, which is the same system read backwards. Their weapons hold a
light with a negative colour, which the renderer *subtracts* from whatever is near it — a swing
takes illumination out of the world instead of adding it, and drags a streak of pitch black behind
it. Black on black cannot be seen, so an enemy's swing reads exactly as far as your own light
reaches.

Two of them share the world. The **crumpy** is the common one: quick, hooked khopesh, long arms.
The **alien** is rare, slow, heavily armoured, and sees furthest. They stand in camps across the
valley and in the garrisons of a ruined keep.

**What you are there for** is the lighthouse. Find the keys, get into the keep, and strike the
beacon at the top of the tower — with the swing, the same verb you have used for everything else.
The eclipse ends.

## Controls

| | |
| --- | --- |
| Move | `WASD` |
| Look | Mouse |
| Swing / use what you are holding | Left mouse |
| Jump | `Space` |
| Sprint | `Left Shift` |
| Interact | `E` |
| Weapon wheel | Hold `Q` |
| Pause | `Escape` |

Holding `Q` slows the game and hands the mouse to a pointer that steers round a dial instead of the
camera; letting go equips whatever it was over. `Escape` stops the world and is also where the
mouse speed slider lives — turn it down first if the camera feels like it is on ice.

## Credits (keep this on the page)

Two of the models in the game are used under Creative Commons Attribution, which means these
lines have to be somewhere the player can read them. Until the game has a credits screen, that
is here.

- **"Mountain Dragon"** by Alexey Zaika — [CC BY 4.0](http://creativecommons.org/licenses/by/4.0/),
  [Sketchfab](https://sketchfab.com/3d-models/mountain-dragon-0a0665396f634b0e9d0872720087f52c).
  Modified: re-exported, rescaled, remade materials, and its single take cut into five clips.
- **"Torch"** by milacetious — [CC BY 4.0](http://creativecommons.org/licenses/by/4.0/),
  [Sketchfab](https://sketchfab.com/3d-models/torch-e34b86256f1f44fba4073fc0ee00fdd7).

With thanks, though none of these ask to be named: animation from Adobe Mixamo and Kevin
Iglesias' *Human Animations*, vegetation wind from Nicrom's *Low Poly Wind*, camera shake from
First Gear Games' *Smooth Camera Shaker*. Built with Unity.

The full record, including the assets whose origin still has to be traced, is in
[THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).

## Honest notes (keep these on the page)

- **There is no sound.** Not a placeholder, not a bug — no audio has been built yet. Play it silent
  or put your own music on.
- This is a prototype. One world, one ending, no save.

---

## Still needed before this page can go up

Two things cannot be written, only captured from a running build, and one is a licence question:

1. **Cover image, 630×500.** itch crops to this, so compose for it rather than cropping a
   screenshot. The keep on the ridge under the eclipse is the shot that says what the game is.
2. **Screenshots — from the built player, not the Scene view.** The ones in
   [README.md](../README.md) are editor captures and say so; a store page wants the game as it
   actually looks, lantern and all. Four is enough: the lantern in the dark, a crumpy mid-swing
   with its black trail, a patch of glow mushrooms, and the lighthouse lit.

3. **The unresolved licences.** The music now in the project and five of the models — the sword,
   the khopesh, the axe, the chain whip and the base mesh all three characters are sculpted from —
   arrived with no licence text and no recorded source. Publishing is distribution, so each one
   needs tracing before this page goes up; `THIRD_PARTY_NOTICES.md` lists the evidence for each.
