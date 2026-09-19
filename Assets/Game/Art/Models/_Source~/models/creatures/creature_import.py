"""Import a shipped creature FBX into a new .blend next to this script.

    blender --background --python creature_import.py -- goblin
    blender --background --python creature_import.py -- mountain_dragon

It is a one-time import for creatures that arrived as an FBX with no Blender source. It refuses to
touch a .blend that already exists, because after the first hand edit the .blend is the source of
truth and re-running this would destroy it.

What it does beyond a plain import, and why:

  * Applies the scale Unity applies (`CREATURES[...]["scale"]`, read from the FBX's .meta), so the
    .blend is in metres like the rest of the library.
  * Removes what the FBX carries that is not the creature: a render studio, lights and camera left
    in the scene by Maya, and stray helper meshes left by Sketchfab (`"discard"`).
  * Rebuilds every material from the maps in `Creatures/<Name>/Textures/`. The importer drops
    Maya's StingrayPBS and Sketchfab's embedded maps, leaving flat grey materials. The maps are
    Unity's packed layout -- metallic in R, smoothness in A -- so roughness is `1 - A`.

Texture paths are made relative so the .blend keeps finding the maps wherever the repository is
checked out.
"""
import sys
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
CREATURES_DIR = HERE.parents[2] / "Creatures"

CREATURES = {
    "goblin": {
        "fbx": "Goblin/Goblin.fbx",
        "scale": 1.0,
        "discard": ["Render_studio", "Render_studio_light", "aiAreaLight1", "aiAreaLight2", "directionalLight1", "Camera_01"],
        "materials": {
            "Armor_Mat_01": {
                "base": "Goblin_Armor_BaseColor.png",
                "packed": "Goblin_Armor_MetallicSmoothness.png",
                "normal": "Goblin_Armor_Normal.png",
            },
            "Goblins_body_Mat_01": {
                "base": "Goblin_Goblins_body_BaseColor.png",
                "packed": "Goblin_Body_MetallicSmoothness.png",
                "normal": "Goblin_Goblins_body_Normal.png",
            },
            "Sword_Shield_01": {
                "base": "Goblin_Sword_Shield_BaseColor.png",
                "packed": "Goblin_SwordShield_MetallicSmoothness.png",
                "normal": "Goblin_Sword_Shield_Normal.png",
            },
        },
    },
    "mountain_dragon": {
        "fbx": "MountainDragon/MountainDragon.fbx",
        "scale": 0.01,
        "discard": ["Icosphere"],
        "materials": {
            "M_MountainDragon": {
                "base": "MountainDragon_BaseColor.jpg",
                "packed": "MountainDragon_MetallicSmoothness.png",
                "normal": "MountainDragon_Normal.jpg",
                "emissive": "MountainDragon_Emissive.jpg",
            },
        },
    },
}


def load_map(textures: Path, filename: str, colorspace: str) -> bpy.types.Image:
    image = bpy.data.images.load(str(textures / filename), check_existing=True)
    image.colorspace_settings.name = colorspace
    image.alpha_mode = "CHANNEL_PACKED"
    return image


def rebuild_material(material: bpy.types.Material, maps: dict, textures: Path) -> None:
    tree = material.node_tree
    tree.nodes.clear()

    def image_node(key: str, colorspace: str, location: tuple) -> bpy.types.Node:
        node = tree.nodes.new("ShaderNodeTexImage")
        node.image = load_map(textures, maps[key], colorspace)
        node.location = location
        return node

    out = tree.nodes.new("ShaderNodeOutputMaterial")
    out.location = (600, 0)
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (300, 0)
    tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    base = image_node("base", "sRGB", (-600, 300))
    tree.links.new(base.outputs["Color"], bsdf.inputs["Base Color"])

    packed = image_node("packed", "Non-Color", (-600, 0))
    split = tree.nodes.new("ShaderNodeSeparateColor")
    split.location = (-300, 0)
    tree.links.new(packed.outputs["Color"], split.inputs["Color"])
    tree.links.new(split.outputs["Red"], bsdf.inputs["Metallic"])
    invert = tree.nodes.new("ShaderNodeInvert")
    invert.location = (-300, -200)
    tree.links.new(packed.outputs["Alpha"], invert.inputs["Color"])
    tree.links.new(invert.outputs["Color"], bsdf.inputs["Roughness"])

    normal = image_node("normal", "Non-Color", (-600, -300))
    normal_map = tree.nodes.new("ShaderNodeNormalMap")
    normal_map.location = (-300, -400)
    tree.links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    tree.links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])

    if "emissive" in maps:
        emissive = image_node("emissive", "sRGB", (-600, -600))
        tree.links.new(emissive.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.0


def import_creature(key: str) -> Path:
    spec = CREATURES[key]
    fbx = CREATURES_DIR / spec["fbx"]
    if not fbx.is_file():
        raise FileNotFoundError(fbx)

    target = HERE / f"{key}.blend"
    if target.exists():
        raise FileExistsError(f"{target} already exists; edit it in place, never regenerate")

    bpy.ops.wm.read_homefile(use_empty=True)
    # Blender 5.1's FBX importer still assigns `light.cycles.cast_shadow`, which Cycles no longer
    # defines, so any FBX carrying a light fails to import. Declaring the property lets it through.
    bpy.ops.preferences.addon_enable(module="cycles")
    probe = bpy.data.lights.new("probe", "POINT")
    type(probe.cycles).cast_shadow = bpy.props.BoolProperty()
    bpy.data.lights.remove(probe)

    bpy.ops.import_scene.fbx(filepath=str(fbx), global_scale=spec["scale"])

    for name in spec["discard"]:
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
    bpy.ops.outliner.orphans_purge(do_recursive=True)

    textures = fbx.parent / "Textures"
    for name, maps in spec["materials"].items():
        rebuild_material(bpy.data.materials[name], maps, textures)

    bpy.ops.wm.save_as_mainfile(filepath=str(target), relative_remap=True)
    return target


if __name__ == "__main__":
    print("wrote", import_creature(sys.argv[sys.argv.index("--") + 1]))
