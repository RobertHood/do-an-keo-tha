# python write_catalog.py <work_dir> <out_dir>
# Combines entries.json (make_jobs.py) and results.json (export_glb.py) into <out_dir>/catalog.json and copies
# the catalog thumbnails to <out_dir>/thumbnails.
import io, json, os, shutil, sys

work, out = sys.argv[1], sys.argv[2]
entries = json.load(io.open(os.path.join(work, "entries.json"), encoding="utf-8"))
results = {r["id"]: r for r in json.load(io.open(os.path.join(work, "results.json"), encoding="utf-8"))}

os.makedirs(os.path.join(out, "thumbnails"), exist_ok=True)
models = []
for e in entries:
    r = results.get(e["id"])
    if not r or not r.get("ok"):
        print("skipped (export failed):", e["id"])
        continue
    thumbnail = e.pop("_thumbnail")
    if os.path.exists(thumbnail):
        shutil.copyfile(thumbnail, os.path.join(out, e["thumbnailUrl"]))
    else:
        print("no thumbnail:", e["id"])
    if not e["loai"] and e["category"] == "Plants":
        e["loai"] = "cay"
    e["fileSize"] = r["bytes"]
    models.append(e)

catalog = {"format": "SuburbsimModelCatalog", "version": 1, "models": models}
with io.open(os.path.join(out, "catalog.json"), "w", encoding="utf-8", newline="\n") as f:
    f.write(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n")
print(len(models), "models,", round(sum(m["fileSize"] for m in models) / 1e6, 1), "MB of GLB")
