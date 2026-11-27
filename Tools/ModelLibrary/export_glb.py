# blender -b --factory-startup --python export_glb.py -- jobs.json results.json tmp_dir
# Converts each FBX to a self-contained GLB with textures capped at MAX_TEX px.
# Reports the model's bounds in Unity space (unity = (-bx, bz, -by)), the same mapping Unity's FBX importer
# and glTFast use, so sizes/pivots can be checked against the catalog's baseSize.
import bpy, sys, os, json, struct, traceback
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
jobs_path, results_path, tmp_root = argv[0], argv[1], argv[2]
MAX_TEX = 1024
GLASS_WORDS = ("glass", "sklo", "window", "water", "voda")

jobs = json.load(open(jobs_path, encoding="utf-8"))
results = []


def unity_bounds():
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    found = False
    deps = bpy.context.evaluated_depsgraph_get()
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        ev = o.evaluated_get(deps)
        for c in ev.bound_box:
            w = o.matrix_world @ Vector(c)
            u = Vector((-w.x, w.z, -w.y))
            lo = Vector(map(min, lo, u)); hi = Vector(map(max, hi, u))
            found = True
    return (list(lo), list(hi)) if found else (None, None)


def fix_bogus_alpha():
    """The FBX importer wires a texture's Color into the BSDF Alpha. That makes the exporter merge colour and
    alpha into new PNGs and mark the material transparent. Only a texture's real Alpha output may drive Alpha."""
    removed = 0
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        nt = mat.node_tree
        for bsdf in [n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"]:
            alpha = bsdf.inputs["Alpha"]
            for link in list(alpha.links):
                if link.from_node.type == "TEX_IMAGE" and link.from_socket.name == "Color":
                    nt.links.remove(link)
                    removed += 1
            if not alpha.is_linked:
                alpha.default_value = 1.0
    return removed


def attach_fallback_texture(path):
    img = bpy.data.images.load(path, check_existing=True)
    if not bpy.data.materials:
        mat = bpy.data.materials.new("tree_diffuse")
        mat.use_nodes = True
    for o in bpy.context.scene.objects:
        if o.type == "MESH" and not any(s.material for s in o.material_slots):
            o.data.materials.clear()
            o.data.materials.append(bpy.data.materials[0])
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        nt = mat.node_tree
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None or bsdf.inputs["Base Color"].is_linked:
            continue
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Base Color"].default_value = (1, 1, 1, 1)


def shrink_textures(tmp_dir):
    os.makedirs(tmp_dir, exist_ok=True)
    missing, resized = [], 0
    for i, img in enumerate(list(bpy.data.images)):
        if img.source != "FILE":
            continue
        w, h = img.size
        if w == 0 or h == 0:
            missing.append(img.filepath)
            continue
        if max(w, h) <= MAX_TEX:
            continue
        ext = os.path.splitext(img.filepath)[1].lower()
        jpeg = ext in (".jpg", ".jpeg")
        s = MAX_TEX / max(w, h)
        img.scale(max(1, round(w * s)), max(1, round(h * s)))
        resized += 1
        out = os.path.join(tmp_dir, "%02d_%s%s" % (i, bpy.path.clean_name(os.path.splitext(img.name)[0]), ".jpg" if jpeg else ".png"))
        img.file_format = "JPEG" if jpeg else "PNG"
        try:
            img.save(filepath=out, quality=85)
        except TypeError:
            img.filepath_raw = out
            img.save()
        img.filepath = out
    return missing, resized


def export_glb(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    opts = dict(filepath=path, export_format="GLB", export_yup=True, export_apply=True,
                export_animations=False, export_cameras=False, export_lights=False,
                export_image_format="AUTO",
                export_draco_mesh_compression_enable=True, export_draco_mesh_compression_level=6,
                export_draco_position_quantization=14, export_draco_normal_quantization=10,
                export_draco_texcoord_quantization=12)
    for drop in ([], ["export_animations"], ["export_animations", "export_lights", "export_cameras"]):
        try:
            bpy.ops.export_scene.gltf(**{k: v for k, v in opts.items() if k not in drop})
            return
        except TypeError:
            continue
    raise RuntimeError("glTF exporter rejected the options")


def fix_alpha(path):
    """Leaves/grass: BLEND sorts badly in real time, use MASK. Glass and water stay BLEND."""
    data = open(path, "rb").read()
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    jlen, jtype = struct.unpack_from("<II", data, 12)
    gltf = json.loads(data[20:20 + jlen].decode("utf-8"))
    rest = data[20 + jlen:]
    changed = []
    for m in gltf.get("materials", []):
        name = (m.get("name") or "").lower()
        glass = any(w in name for w in GLASS_WORDS)
        pbr = m.setdefault("pbrMetallicRoughness", {})
        if glass and "baseColorTexture" not in pbr:
            color = pbr.get("baseColorFactor", [0.8, 0.8, 0.8, 1.0])
            if color[3] > 0.6:
                pbr["baseColorFactor"] = color[:3] + [0.35]
            m["alphaMode"] = "BLEND"
            m.pop("alphaCutoff", None)
            changed.append(m.get("name") + " (glass)")
        elif m.get("alphaMode") == "BLEND" and not glass:
            m["alphaMode"] = "MASK"
            m["alphaCutoff"] = 0.5
            changed.append(m.get("name"))
    if changed:
        js = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
        js += b" " * ((4 - len(js) % 4) % 4)
        total = 12 + 8 + len(js) + len(rest)
        open(path, "wb").write(struct.pack("<4sII", magic, version, total) + struct.pack("<II", len(js), jtype) + js + rest)
    blend = [m.get("name") for m in gltf.get("materials", []) if m.get("alphaMode") == "BLEND"]
    return changed, blend, len(gltf.get("materials", [])), len(gltf.get("images", []))


for job in jobs:
    res = {"id": job["id"]}
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=job["fbx"])
        res["bogusAlphaLinks"] = fix_bogus_alpha()
        if job.get("texture"):
            attach_fallback_texture(job["texture"])
        res["missingTextures"], res["resizedTextures"] = shrink_textures(os.path.join(tmp_root, job["id"]))
        res["boundsMin"], res["boundsMax"] = unity_bounds()
        export_glb(job["out"])
        res["alphaToMask"], res["alphaBlend"], res["materials"], res["images"] = fix_alpha(job["out"])

        # Round trip: the GLB must come back with the same bounds
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=job["out"])
        res["glbMin"], res["glbMax"] = unity_bounds()
        res["bytes"] = os.path.getsize(job["out"])
        res["ok"] = True
    except Exception:
        res["ok"] = False
        res["error"] = traceback.format_exc()
    results.append(res)
    print("DONE", job["id"], res.get("ok"), res.get("bytes"))

json.dump(results, open(results_path, "w", encoding="utf-8"), indent=1)
