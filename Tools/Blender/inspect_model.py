"""Print mesh, bone, material and texture statistics of an FBX file.

Usage:
    blender --background --factory-startup --python Tools/Blender/inspect_model.py -- <path/to/model.fbx>
"""
import sys

import bpy


def main():
    path = sys.argv[sys.argv.index("--") + 1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)

    total_tris = 0
    total_verts = 0
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            mesh = obj.data
            mesh.calc_loop_triangles()
            tris = len(mesh.loop_triangles)
            total_tris += tris
            total_verts += len(mesh.vertices)
            # Largest number of bones influencing a single vertex.
            max_influences = max((sum(1 for g in v.groups if g.weight > 0.0) for v in mesh.vertices), default=0)
            print(f"MESH {obj.name}: {tris} tris, {len(mesh.vertices)} verts, "
                  f"{len(mesh.uv_layers)} UV sets, {len(obj.vertex_groups)} vertex groups, "
                  f"max influences/vertex {max_influences}, "
                  f"materials {[s.material.name if s.material else None for s in obj.material_slots]}")
        elif obj.type == "ARMATURE":
            bones = obj.data.bones
            print(f"ARMATURE {obj.name}: {len(bones)} bones")
            print("  " + ", ".join(b.name for b in bones))

    for mat in bpy.data.materials:
        images = []
        if mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image:
                    img = node.image
                    images.append(f"{img.name} {img.size[0]}x{img.size[1]}")
        print(f"MATERIAL {mat.name}: {images}")

    print(f"TOTAL {total_tris} tris, {total_verts} verts")


main()
