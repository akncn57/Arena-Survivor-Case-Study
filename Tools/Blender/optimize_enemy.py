"""Build the mobile-optimized enemy from the original enemy.fbx. The original file is only read.

Steps:
  1. Merge finger weights into the hands, then delete the 40 finger bones and the 3 unweighted end bones.
  2. Limit skin weights to 4 bones per vertex (Unity's maximum for mobile quality settings).
  3. Merge the two materials into one: each material's UVs (both 0..1) move into one half of an atlas.
  4. Create LOD0 and LOD1 with the Decimate (collapse) modifier. Names ending in _LOD0/_LOD1 make Unity
     build an LODGroup on import.
  5. Build a diffuse and a normal atlas (two 4096 textures side by side, scaled down). Specular and
     glossiness maps are dropped (the specular maps are flat; a constant smoothness is used instead).
  6. Export the armature and both LODs to FBX (no animation, no leaf bones, no embedded textures).

Usage:
    blender --background --factory-startup --python Tools/Blender/optimize_enemy.py -- <source.fbx> <output folder>
"""
import os
import sys

import bpy
import numpy as np

LOD_TRIANGLES = (4500, 1500)
ATLAS_HALF = 512                     # each original 4096 texture becomes 512 x 512 in the atlas
MAX_BONE_INFLUENCES = 4
FINGER_KEYWORDS = ("Thumb", "Index", "Middle", "Ring", "Pinky")
END_BONES = ("mixamorig:HeadTop_End", "mixamorig:LeftToe_End", "mixamorig:RightToe_End")

# Which half of the atlas each original material goes to, and its texture prefix in enemy.fbm.
ATLAS_LAYOUT = {
    "Ch30_Body1": (0, "Ch30_1002"),  # left half: body (14,244 faces)
    "Ch30_Body": (1, "Ch30_1001"),   # right half: 4,234 faces
}


def log(message):
    print("OPT " + message, flush=True)


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def strip_finger_bones(mesh_obj, armature_obj):
    groups = mesh_obj.vertex_groups
    moved = 0
    for side in ("Left", "Right"):
        hand = groups.get(f"mixamorig:{side}Hand")
        fingers = [g for g in groups if g.name.startswith(f"mixamorig:{side}Hand") and any(k in g.name for k in FINGER_KEYWORDS)]
        for finger in fingers:
            for v in mesh_obj.data.vertices:
                for g in v.groups:
                    if g.group == finger.index and g.weight > 0.0:
                        hand.add([v.index], g.weight, "ADD")
                        moved += 1
            groups.remove(finger)
    log(f"finger weights moved to hands: {moved} vertex weights")

    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = armature_obj.data.edit_bones
    doomed = [b for b in edit_bones if any(k in b.name for k in FINGER_KEYWORDS) or b.name in END_BONES]
    for bone in doomed:
        edit_bones.remove(bone)
    bpy.ops.object.mode_set(mode="OBJECT")
    log(f"bones removed: {len(doomed)}, remaining: {len(armature_obj.data.bones)}")


def limit_weights(mesh_obj):
    bpy.context.view_layer.objects.active = mesh_obj
    mesh_obj.select_set(True)
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=MAX_BONE_INFLUENCES)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    worst = max(sum(1 for g in v.groups if g.weight > 0.0) for v in mesh_obj.data.vertices)
    log(f"max influences per vertex: {worst}")


def merge_materials_into_atlas(mesh_obj):
    mesh = mesh_obj.data
    uv = mesh.uv_layers.active.data
    half_of_slot = {i: ATLAS_LAYOUT[s.material.name][0] for i, s in enumerate(mesh_obj.material_slots)}
    for poly in mesh.polygons:
        half = half_of_slot[poly.material_index]
        for li in poly.loop_indices:
            u, v = uv[li].uv
            uv[li].uv = (u * 0.5 + 0.5 * half, v)
        poly.material_index = 0

    material = bpy.data.materials.new("M_Enemy")
    mesh.materials.clear()
    mesh.materials.append(material)
    log("materials merged into one, UVs moved into atlas halves")


def make_lod(source, name, target_tris):
    lod = source.copy()
    lod.data = source.data.copy()
    lod.name = name
    lod.data.name = name
    bpy.context.collection.objects.link(lod)

    ratio = min(1.0, target_tris / triangle_count(source))
    modifier = lod.modifiers.new("Decimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    # The armature modifier must stay last in the stack; apply only the decimation.
    bpy.context.view_layer.objects.active = lod
    bpy.ops.object.modifier_move_to_index(modifier="Decimate", index=0)
    bpy.ops.object.modifier_apply(modifier="Decimate")
    log(f"{name}: {triangle_count(lod)} tris, {len(lod.data.vertices)} verts (ratio {ratio:.3f})")
    return lod


def build_atlas(texture_dir, kind, output_path, is_normal):
    atlas = np.zeros((ATLAS_HALF, ATLAS_HALF * 2, 4), dtype=np.float32)
    for _, (half, prefix) in ATLAS_LAYOUT.items():
        image = bpy.data.images.load(os.path.join(texture_dir, f"{prefix}_{kind}.png"))
        if is_normal:
            image.colorspace_settings.name = "Non-Color"
        image.scale(ATLAS_HALF, ATLAS_HALF)
        pixels = np.empty(ATLAS_HALF * ATLAS_HALF * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        atlas[:, half * ATLAS_HALF:(half + 1) * ATLAS_HALF, :] = pixels.reshape(ATLAS_HALF, ATLAS_HALF, 4)
        bpy.data.images.remove(image)

    result = bpy.data.images.new(os.path.basename(output_path), ATLAS_HALF * 2, ATLAS_HALF, alpha=False,
                                 is_data=is_normal)
    result.pixels.foreach_set(atlas.ravel())
    result.filepath_raw = output_path
    result.file_format = "PNG"
    result.save()
    log(f"atlas written: {output_path} ({ATLAS_HALF * 2}x{ATLAS_HALF})")


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    source_path, output_dir = args[0], args[1]
    os.makedirs(output_dir, exist_ok=True)
    texture_dir = os.path.join(os.path.dirname(source_path), "enemy.fbm")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source_path)
    armature = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    mesh_obj = next(o for o in bpy.context.scene.objects if o.type == "MESH")
    log(f"source: {triangle_count(mesh_obj)} tris, {len(mesh_obj.data.vertices)} verts, "
        f"{len(armature.data.bones)} bones, {len(mesh_obj.material_slots)} materials")

    strip_finger_bones(mesh_obj, armature)
    limit_weights(mesh_obj)
    merge_materials_into_atlas(mesh_obj)

    lods = [make_lod(mesh_obj, f"Enemy_LOD{i}", tris) for i, tris in enumerate(LOD_TRIANGLES)]
    bpy.data.objects.remove(mesh_obj)

    build_atlas(texture_dir, "Diffuse", os.path.join(output_dir, "Enemy_Diffuse.png"), is_normal=False)
    build_atlas(texture_dir, "Normal", os.path.join(output_dir, "Enemy_Normal.png"), is_normal=True)

    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    for lod in lods:
        lod.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(output_dir, "Enemy_Optimized.fbx"),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
        mesh_smooth_type="FACE",
    )
    log("exported Enemy_Optimized.fbx")


main()
