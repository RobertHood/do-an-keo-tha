# blender -b --factory-startup --python measure_materials.py -- <out.json> a.glb b.glb ...
# Measures every render material of each GLB (metres at scale 1) as a starting point for the houses' building
# material lists: total surface area, area facing up / down (floors, ceilings), and the number of separate pieces
# (e.g. glass panes). The numbers describe the visual model, not a construction drawing: a pane is a thin box (both
# faces and edges are counted), walls are counted inside and outside, some floors face down. Check them by hand.
import bmesh, bpy, io, json, os, sys
from collections import defaultdict

args = sys.argv[sys.argv.index("--") + 1:]
out_path, glbs = args[0], args[1:]
result = {}

for glb in glbs:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    area, up, down, parts = defaultdict(float), defaultdict(float), defaultdict(float), defaultdict(int)

    for ob in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
        names = [s.material.name if s.material else "(none)" for s in ob.material_slots] or ["(none)"]
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bm.transform(ob.matrix_world)
        bm.normal_update()
        name_of = lambda f: names[min(f.material_index, len(names) - 1)]

        for f in bm.faces:
            a = f.calc_area()
            n = name_of(f)
            area[n] += a
            if f.normal.z > 0.9:
                up[n] += a
            elif f.normal.z < -0.9:
                down[n] += a

        seen = set()
        for f in bm.faces:
            if f.index in seen:
                continue
            seen.add(f.index)
            stack = [f]
            while stack:
                g = stack.pop()
                for e in g.edges:
                    for h in e.link_faces:
                        if h.index not in seen and h.material_index == f.material_index:
                            seen.add(h.index)
                            stack.append(h)
            parts[name_of(f)] += 1
        bm.free()

    model_id = os.path.splitext(os.path.basename(glb))[0]
    result[model_id] = [
        {
            "sourceMaterial": n,
            "area": round(area[n], 2),
            "upArea": round(up[n], 2),
            "downArea": round(down[n], 2),
            "parts": parts[n],
        }
        for n in sorted(area, key=lambda k: -area[k])
    ]
    print(model_id, len(result[model_id]), "materials")

with io.open(out_path, "w", encoding="utf-8", newline="\n") as f:
    f.write(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
