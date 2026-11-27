# blender -b --factory-startup --python render_glb.py -- out_dir a.glb b.glb ...
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir, files = argv[0], argv[1:]
os.makedirs(out_dir, exist_ok=True)

for f in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 412, 240
    world = bpy.data.worlds.new("w"); scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.8, 0.88, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.2
    bpy.ops.import_scene.gltf(filepath=f)
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for o in scene.objects:
        if o.type == "MESH":
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c)
                lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    center, radius = (lo + hi) / 2, (hi - lo).length / 2
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam); scene.camera = cam
    # Same viewpoint as the catalog thumbnails: front-left, from above (Blender -Y is the model front)
    d = Vector((-1.0, -1.2, 0.7)).normalized()
    cam.location = center + d * radius * 2.6
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(out_dir, os.path.splitext(os.path.basename(f))[0] + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)
