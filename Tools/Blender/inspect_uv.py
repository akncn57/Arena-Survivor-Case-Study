"""Print the UV range and triangle count of each material slot of the first mesh in an FBX file.

Usage:
    blender --background --factory-startup --python Tools/Blender/inspect_uv.py -- <path/to/model.fbx>
"""
import sys

import bpy


def main():
    path = sys.argv[sys.argv.index("--") + 1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)

    obj = next(o for o in bpy.context.scene.objects if o.type == "MESH")
    mesh = obj.data
    uv = mesh.uv_layers.active.data
    for slot_index, slot in enumerate(obj.material_slots):
        us, vs, faces = [], [], 0
        for poly in mesh.polygons:
            if poly.material_index != slot_index:
                continue
            faces += 1
            for li in poly.loop_indices:
                us.append(uv[li].uv.x)
                vs.append(uv[li].uv.y)
        print(f"SLOT {slot_index} {slot.material.name}: {faces} faces, "
              f"u {min(us):.3f}..{max(us):.3f}, v {min(vs):.3f}..{max(vs):.3f}")

    # Weight summary: vertices by number of non-zero influences.
    histogram = {}
    for v in mesh.vertices:
        n = sum(1 for g in v.groups if g.weight > 0.0)
        histogram[n] = histogram.get(n, 0) + 1
    print("INFLUENCES", dict(sorted(histogram.items())))

    finger_groups = [g.name for g in obj.vertex_groups if any(f in g.name for f in ("Thumb", "Index", "Middle", "Ring", "Pinky"))]
    print("FINGER GROUPS", len(finger_groups))


main()
