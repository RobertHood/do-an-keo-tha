"""Converts every downloaded BlenderKit model (ModelSources/blenderkit/<Category>/<model>/source.blend)
into a web-ready FBX with textures, using export_for_unity.py in background Blender.

    python Tools/Blender/convert_all.py <blender.exe> <sources dir> <output dir>

Output: <output dir>/<Category>/<model>/<model>.fbx + Textures/ + info.json (BlenderKit metadata) + export.json.
Models that already have an FBX in the output are skipped."""
import json, os, shutil, subprocess, sys

blender, sources, output = sys.argv[1:4]
here = os.path.dirname(os.path.abspath(__file__))
script = os.path.join(here, "export_for_unity.py")

# Triangle budget and max texture size per category (keeps the WebGL build small)
BUDGET = {
    "House": (80000, 1024),
    "Plants": (40000, 512),
    "GardenFurniture": (20000, 512),
    "Decor": (15000, 512),
    "Lighting": (15000, 512),
    "FenceGate": (15000, 512),
    "PathWater": (25000, 1024),
}

results = []
for category in sorted(os.listdir(sources)):
    cat_dir = os.path.join(sources, category)
    if not os.path.isdir(cat_dir):
        continue
    tris, tex = BUDGET.get(category, (20000, 512))
    for model in sorted(os.listdir(cat_dir)):
        src = os.path.join(cat_dir, model, "source.blend")
        info = os.path.join(cat_dir, model, "info.json")
        if not os.path.exists(src):
            continue
        out = os.path.join(output, category, model)
        if os.path.exists(os.path.join(out, model + ".fbx")):
            results.append((category, model, "skipped (exists)"))
            continue
        os.makedirs(out, exist_ok=True)
        proc = subprocess.run([blender, "-b", src, "--python", script, "--", out, model, str(tris), str(tex)],
                              capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=900)
        line = next((l for l in proc.stdout.splitlines() if l.startswith("[export] ok")), None)
        if line and os.path.exists(os.path.join(out, model + ".fbx")):
            shutil.copy(info, os.path.join(out, "info.json"))
            results.append((category, model, line[len("[export] ok "):]))
        else:
            errors = [l for l in (proc.stdout + proc.stderr).splitlines() if "Error" in l or "[export]" in l][-3:]
            results.append((category, model, "FAILED " + " | ".join(errors)))
        print(results[-1][0], results[-1][1], results[-1][2][:160], flush=True)

failed = [r for r in results if r[2].startswith("FAILED")]
print(f"\n{len(results) - len(failed)} converted, {len(failed)} failed")
