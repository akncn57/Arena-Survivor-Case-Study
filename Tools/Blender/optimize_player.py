"""Build the mobile-optimized player from the original player.fbx. The original file is only read.

Steps:
  1. Join the head and body meshes into one skinned mesh (one renderer instead of two).
  2. Remove the two unused UV sets (only the first one is sampled by the materials).
  3. Merge the two body materials (they use the same textures) into one: 3 material slots become 2.
     Head and body are NOT merged into an atlas: the body UVs tile outside 0..1, which an atlas would break,
     and one extra draw call for a single character is negligible.
  4. Decimate from 19,450 to about 8,000 triangles. No LODs: the player is always at the same distance.
  5. Finger bones are KEPT: there is only one player, so their cost is negligible, while the hand gripping
     the rifle is visible. (The enemy loses its fingers because there are 150 of them.)
  6. Diffuse textures scaled from 1024 to 512 (normal maps are already 512). Specular maps are dropped;
     the material uses a constant smoothness.

Usage:
    blender --background --factory-startup --python Tools/Blender/optimize_player.py -- <source.fbx> <output folder>
"""
import os
import sys

import bpy

TARGET_TRIANGLES = 8000
DIFFUSE_SIZE = 512
BODY_MATERIALS = ("Soldier_body1", "Soldier_body1.001")


def log(message):
    print("OPT " + message, flush=True)


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def join_meshes(meshes):
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    # Not "Player": the scene's root object is called that, and GameObject.Find would be ambiguous.
    joined.name = "PlayerMesh"
    joined.data.name = "PlayerMesh"
    log(f"joined {len(meshes)} meshes: {triangle_count(joined)} tris")
    return joined


def remove_extra_uv_sets(obj):
    layers = obj.data.uv_layers
    keep = layers[0].name
    for layer in [l for l in layers if l.name != keep]:
        layers.remove(layer)
    log(f"UV sets kept: {[l.name for l in obj.data.uv_layers]}")


def merge_body_materials(obj):
    body = bpy.data.materials.new("M_PlayerBody")
    head = bpy.data.materials.new("M_PlayerHead")
    slot_target = {}
    for i, slot in enumerate(obj.material_slots):
        slot_target[i] = 0 if slot.material.name in BODY_MATERIALS else 1
    # materials.clear() resets every face's material index, so the new indices are applied afterwards.
    new_indices = [slot_target[poly.material_index] for poly in obj.data.polygons]
    obj.data.materials.clear()
    obj.data.materials.append(body)
    obj.data.materials.append(head)
    for poly, index in zip(obj.data.polygons, new_indices):
        poly.material_index = index
    log(f"materials: M_PlayerBody ({new_indices.count(0)} faces), M_PlayerHead ({new_indices.count(1)} faces)")


def decimate(obj, target):
    ratio = min(1.0, target / triangle_count(obj))
    modifier = obj.modifiers.new("Decimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_move_to_index(modifier="Decimate", index=0)
    bpy.ops.object.modifier_apply(modifier="Decimate")
    log(f"decimated: {triangle_count(obj)} tris, {len(obj.data.vertices)} verts (ratio {ratio:.3f})")


def write_texture(source_path, output_path, size, is_normal):
    image = bpy.data.images.load(source_path)
    if is_normal:
        image.colorspace_settings.name = "Non-Color"
    _ = image.pixels[0]  # Blender loads pixels lazily; without this an unscaled image saves empty.
    if size and image.size[0] > size:
        image.scale(size, size)
    image.filepath_raw = output_path
    image.file_format = "PNG"
    image.save()
    log(f"texture written: {os.path.basename(output_path)} {image.size[0]}x{image.size[1]}")
    bpy.data.images.remove(image)


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    source_path, output_dir = args[0], args[1]
    os.makedirs(output_dir, exist_ok=True)
    texture_dir = os.path.join(os.path.dirname(source_path), "player.fbm")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source_path)
    armature = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    log(f"source: {sum(triangle_count(m) for m in meshes)} tris in {len(meshes)} meshes, {len(armature.data.bones)} bones")

    player = join_meshes(meshes)
    remove_extra_uv_sets(player)
    merge_body_materials(player)
    decimate(player, TARGET_TRIANGLES)

    for part in ("Body", "head"):
        write_texture(os.path.join(texture_dir, f"Soldier_{part}_diffuse.png"),
                      os.path.join(output_dir, f"Player_{part.capitalize()}_Diffuse.png"), DIFFUSE_SIZE, False)
        write_texture(os.path.join(texture_dir, f"Soldier_{part}_normal.png"),
                      os.path.join(output_dir, f"Player_{part.capitalize()}_Normal.png"), None, True)

    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    player.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(output_dir, "Player_Optimized.fbx"),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
        mesh_smooth_type="FACE",
    )
    log("exported Player_Optimized.fbx")


main()
