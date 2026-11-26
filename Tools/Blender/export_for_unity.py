"""Converts a downloaded BlenderKit .blend into a web-ready FBX for the Suburbsim Unity project.

Run with Blender in the background (no window):
    blender -b <source.blend> --python export_for_unity.py -- <output folder> <name> <max triangles> [texture size]

What it does:
- makes collection instances real and applies modifiers / geometry nodes, keeps only meshes,
- joins everything into one object with real-world scale (meters), pivot at the bottom center,
- reduces the triangle count to the budget (Decimate) when the model is heavier,
- shrinks textures to at most <texture size> px (default 1024) and saves them next to the FBX,
- exports <output folder>/<name>.fbx with Unity's axes (Y up, scale 1) and writes export.json with stats.
"""
import bpy, sys, os, json
import numpy
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:]
out_dir, name, max_tris = args[0], args[1], int(args[2])
tex_size = int(args[3]) if len(args) > 3 else 1024
os.makedirs(out_dir, exist_ok=True)
tex_dir = os.path.join(out_dir, "Textures")
os.makedirs(tex_dir, exist_ok=True)

def log(*a):
    print("[export]", *a, flush=True)

scene = bpy.context.scene
view_layer = bpy.context.view_layer

# Everything that is visible in the scene belongs to the asset
for obj in list(scene.objects):
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.hide_select = False

bpy.ops.object.select_all(action="DESELECT")
for obj in scene.objects:
    if obj.type == "EMPTY" and obj.instance_type == "COLLECTION" and obj.instance_collection:
        obj.select_set(True)
if bpy.context.selected_objects:
    view_layer.objects.active = bpy.context.selected_objects[0]
    bpy.ops.object.duplicates_make_real(use_base_parent=False, use_hierarchy=False)

# Apply modifiers / geometry nodes by converting to mesh
bpy.ops.object.select_all(action="DESELECT")
convertible = [o for o in scene.objects if o.type in {"MESH", "CURVE", "SURFACE", "META", "FONT", "CURVES"} and o.visible_get()]
for o in convertible:
    o.select_set(True)
if convertible:
    view_layer.objects.active = convertible[0]
    bpy.ops.object.convert(target="MESH")

# Drop everything that isn't a visible mesh with faces
for obj in list(scene.objects):
    if obj.type != "MESH" or not obj.visible_get() or len(obj.data.polygons) == 0:
        bpy.data.objects.remove(obj, do_unlink=True)

meshes = [o for o in scene.objects if o.type == "MESH"]
if not meshes:
    log("no meshes in file"); sys.exit(3)

# Clear parents (keeping transforms) and join into one object
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
if len(meshes) > 1:
    bpy.ops.object.join()
obj = view_layer.objects.active
obj.name = name
obj.data.name = name
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def triangle_count(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)

tris_before = triangle_count(obj)
if tris_before > max_tris:
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.ratio = max(0.02, max_tris / tris_before)
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
tris_after = triangle_count(obj)

# Pivot at the bottom center, model at the origin
coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
min_v = Vector((min(c.x for c in coords), min(c.y for c in coords), min(c.z for c in coords)))
max_v = Vector((max(c.x for c in coords), max(c.y for c in coords), max(c.z for c in coords)))
offset = Vector(((min_v.x + max_v.x) / 2, (min_v.y + max_v.y) / 2, min_v.z))
obj.data.transform(Matrix.Translation(-offset))
obj.location = (0, 0, 0)
size = max_v - min_v

# ---------------------------------------------------------------- materials
# Blender's FBX exporter only exports textures wired straight into a Principled BSDF, and BlenderKit materials are
# often custom shader groups, AO mixes or packed channel maps. So every material is rebuilt in a simple standard form:
# color image -> Base Color (opacity merged into its alpha), normal image -> Normal Map -> Normal.
import re

COLOR_NAMES = re.compile(r"(base.?colou?r|albedo|_alb|diff|_col\b|_col[._]|_bc[._\b]|_bc$|colou?r)", re.I)
NORMAL_NAMES = re.compile(r"(normal|_nrm|_nor|_nm[._\b]|_nm$|_n[._]\d|_n\.)", re.I)
OPACITY_NAMES = re.compile(r"(opacity|_opc|_op[._\b]|_op$|alpha|mask|cutout)", re.I)
DATA_SPACES = {"Non-Color", "Linear", "Raw", "Non-Colour Data"}

def image_nodes(tree, depth=0):
    """Image nodes in a node tree, including inside node groups."""
    found = []
    for node in tree.nodes:
        if node.type == "TEX_IMAGE" and node.image:
            found.append(node)
        elif node.type == "GROUP" and node.node_tree and depth < 3:
            found.extend(image_nodes(node.node_tree, depth + 1))
    return found

def upstream_images(socket, depth=0):
    """Images feeding a socket through any chain of nodes."""
    found, stack, seen = [], [l.from_node for l in socket.links], set()
    while stack:
        node = stack.pop(0)
        if node in seen:
            continue
        seen.add(node)
        if node.type == "TEX_IMAGE" and node.image:
            found.append(node.image)
        for inp in node.inputs:
            stack.extend(l.from_node for l in inp.links)
    return found

def flat_image(img):
    """UDIM (tiled) textures can't be exported as one file: unpack them and use the first tile instead."""
    if img is None or img.source != "TILED":
        return img
    if len(img.tiles) > 1:
        log("texture has", len(img.tiles), "UDIM tiles, using the first:", img.name)
    if img.packed_file or len(getattr(img, "packed_files", [])) > 0:
        img.unpack(method="WRITE_LOCAL")
    tile = img.tiles[0].number if len(img.tiles) else 1001
    path = bpy.path.abspath(img.filepath_raw or img.filepath).replace("<UDIM>", str(tile))
    if not os.path.exists(path):
        log("missing UDIM tile file:", path)
        return None
    flat = bpy.data.images.load(path)
    flat.colorspace_settings.name = img.colorspace_settings.name
    flat.alpha_mode = img.alpha_mode
    flat.name = img.name.replace(".<UDIM>", "").replace("<UDIM>", "")
    return flat

def pixels_of(img, width, height):
    """RGBA float pixels of an image resized to width x height."""
    copy = img.copy()
    if tuple(copy.size) != (width, height):
        copy.scale(width, height)
    px = numpy.empty(width * height * 4, dtype=numpy.float32)
    copy.pixels.foreach_get(px)
    bpy.data.images.remove(copy)
    return px

def analyse(mat):
    """Finds the color / normal / opacity images and the plain colors of a material."""
    tree = mat.node_tree
    principled = next((n for n in tree.nodes if n.type == "BSDF_PRINCIPLED" and n.outputs[0].links), None)
    principled = principled or next((n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    info = {"color": None, "normal": None, "opacity": None, "base": tuple(mat.diffuse_color),
            "metallic": 0.0, "roughness": 0.6, "alpha": 1.0, "emission": None}

    if principled is not None:
        info["base"] = tuple(principled.inputs["Base Color"].default_value)
        info["metallic"] = principled.inputs["Metallic"].default_value
        info["roughness"] = principled.inputs["Roughness"].default_value
        info["alpha"] = principled.inputs["Alpha"].default_value
        color = [i for i in upstream_images(principled.inputs["Base Color"]) if i.colorspace_settings.name not in DATA_SPACES]
        info["color"] = color[0] if color else None
        # Only a real Normal Map node's image is a normal map (Bump nodes often reuse the color image as height)
        normal = []
        stack, seen = [l.from_node for l in principled.inputs["Normal"].links], set()
        while stack:
            node = stack.pop(0)
            if node in seen:
                continue
            seen.add(node)
            if node.type == "NORMAL_MAP":
                normal = upstream_images(node.inputs["Color"])
                break
            for inp in node.inputs:
                stack.extend(l.from_node for l in inp.links)
        normal = [i for i in normal if i != info["color"]]
        info["normal"] = normal[0] if normal else None
        opacity = upstream_images(principled.inputs["Alpha"])
        info["opacity"] = opacity[0] if opacity else None

    # Custom shader groups: pick images by their usual BlenderKit / PBR names
    all_images = [n.image for n in image_nodes(tree)]
    if info["color"] is None:
        named = [i for i in all_images if COLOR_NAMES.search(i.name) and i.colorspace_settings.name not in DATA_SPACES]
        srgb = [i for i in all_images if i.colorspace_settings.name not in DATA_SPACES and not OPACITY_NAMES.search(i.name)]
        info["color"] = (named or srgb or [None])[0]
    if info["normal"] is None:
        info["normal"] = next((i for i in all_images if NORMAL_NAMES.search(i.name) and i != info["color"]), None)
    if info["opacity"] is None:
        info["opacity"] = next((i for i in all_images if OPACITY_NAMES.search(i.name) and i != info["color"]), None)
    return info

def rebuild(mat, info):
    """Replaces the material's nodes with: image -> Principled BSDF -> Output."""
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Base Color"].default_value = info["base"]
    bsdf.inputs["Metallic"].default_value = info["metallic"]
    bsdf.inputs["Roughness"].default_value = info["roughness"]
    bsdf.inputs["Alpha"].default_value = info["alpha"]

    color = flat_image(info["color"])
    opacity = flat_image(info["opacity"])
    normal = flat_image(info["normal"])

    if color is not None and opacity is not None:
        # Merge the opacity map into the color texture's alpha (Unity reads cut-out from the base map alpha)
        try:
            w, h = color.size
            if max(w, h) > tex_size:
                s = tex_size / max(w, h)
                w, h = max(1, int(w * s)), max(1, int(h * s))
            rgba = pixels_of(color, w, h)
            alpha = pixels_of(opacity, w, h)
            rgba[3::4] = alpha[0::4]
            merged = bpy.data.images.new(color.name + "_A", w, h, alpha=True)
            merged.colorspace_settings.name = "sRGB"
            merged.pixels.foreach_set(rgba)
            merged.alpha_mode = "STRAIGHT"
            color = merged
        except Exception as e:
            log("could not merge opacity:", mat.name, e)

    if color is not None:
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.image = color
        tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if opacity is not None or (color.alpha_mode != "NONE" and color.channels == 4 and mat.blend_method != "OPAQUE"):
            tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if normal is not None:
        normal.colorspace_settings.name = "Non-Color"
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.image = normal
        nmap = tree.nodes.new("ShaderNodeNormalMap")
        tree.links.new(tex.outputs["Color"], nmap.inputs["Color"])
        tree.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return [i for i in (color, normal) if i is not None]

images = set()
color_textures = 0
for slot in obj.material_slots:
    mat = slot.material
    if not mat:
        continue
    if not mat.use_nodes:
        mat.use_nodes = True
    used = rebuild(mat, analyse(mat))
    images.update(used)
    if any(i.colorspace_settings.name not in DATA_SPACES for i in used):
        color_textures += 1

# Textures: shrink and save next to the FBX so Unity finds them
saved = 0
for img in images:
    try:
        if img.size[0] == 0:
            img.reload()
        w, h = img.size
        if w == 0 or h == 0:
            continue
        if max(w, h) > tex_size:
            scale = tex_size / max(w, h)
            img.scale(max(1, int(w * scale)), max(1, int(h * scale)))
        # JPEG keeps the files small; PNG only when the color texture really uses transparency
        is_data = img.colorspace_settings.name in DATA_SPACES
        has_alpha = False
        if not is_data and img.channels == 4:
            pixels = numpy.empty(len(img.pixels), dtype=numpy.float32)
            img.pixels.foreach_get(pixels)
            has_alpha = bool((pixels[3::4] < 0.99).any())
        ext = ".png" if has_alpha else ".jpg"
        safe = "".join(ch if ch.isalnum() or ch in "-_" else "_" for ch in os.path.splitext(img.name)[0])[:60]
        path = os.path.join(tex_dir, safe + ext)
        img.filepath_raw = path
        img.file_format = "PNG" if ext == ".png" else "JPEG"
        img.save()
        saved += 1
    except Exception as e:
        log("texture failed:", img.name, e)

materials = [s.material.name for s in obj.material_slots if s.material]

fbx_path = os.path.join(out_dir, name + ".fbx")
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
view_layer.objects.active = obj
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={"MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    path_mode="RELATIVE",
    embed_textures=False,
)

stats = {
    "name": name,
    "trianglesBefore": tris_before,
    "triangles": tris_after,
    "size": [round(size.x, 3), round(size.z, 3), round(size.y, 3)],  # Unity order: width, height, depth
    "materials": materials,
    "textures": saved,
    "colorTextures": color_textures,
}
json.dump(stats, open(os.path.join(out_dir, "export.json"), "w", encoding="utf-8"), indent=1)
log("ok", json.dumps(stats))
