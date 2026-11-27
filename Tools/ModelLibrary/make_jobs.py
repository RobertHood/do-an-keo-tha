# python make_jobs.py <unity_project> <out_dir> <work_dir>
# Reads PriceCatalog.asset + info.json files and writes jobs.json (for Blender) and entries.json (catalog data).
import io, json, os, re, sys

project, out_dir, work = sys.argv[1], sys.argv[2], sys.argv[3]
assets = os.path.join(project, "Assets")
CATEGORIES = {0: "Uncategorized", 1: "House", 2: "Plants", 3: "GardenFurniture", 4: "Decor",
              5: "Lighting", 6: "FenceGate", 7: "PathWater"}
LOAI = ["", "nha", "cong", "hang_rao", "cay", "chau_cay", "ho_nuoc", "dai_phun_nuoc", "be_boi",
        "loi_di", "den", "tuong", "ghe_ban", "khac"]

guid2path = {}
for root, _, files in os.walk(assets):
    for f in files:
        if f.endswith(".meta"):
            p = os.path.join(root, f)
            with io.open(p, encoding="utf-8", errors="ignore") as fh:
                for line in fh:
                    if line.startswith("guid:"):
                        guid2path[line.split()[1]] = p[:-5]
                        break


def yaml_str(raw):
    raw = raw.strip()
    if raw.startswith('"') and raw.endswith('"'):
        body = raw[1:-1]
        return body.encode("latin-1", "backslashreplace").decode("unicode_escape")
    if raw.startswith("'") and raw.endswith("'"):
        return raw[1:-1].replace("''", "'")
    return raw


def vec(block, key):
    m = re.search(key + r": \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", block)
    return {"x": float(m.group(1)), "y": float(m.group(2)), "z": float(m.group(3))} if m else {"x": 0, "y": 0, "z": 0}


def field(block, key):
    m = re.search(r"\n    " + key + r": (.*)", block)
    return m.group(1) if m else ""


def source_fbx(prefab_path):
    if prefab_path.lower().endswith(".fbx"):
        return prefab_path
    text = io.open(prefab_path, encoding="utf-8").read()
    for g in sorted(set(re.findall(r"guid: (\w+)", text))):
        p = guid2path.get(g, "")
        if p.lower().endswith(".fbx"):
            return p
    return None


asset = io.open(os.path.join(assets, "Prefabs", "PriceCatalog.asset"), encoding="utf-8").read()
blocks = asset.split("\n  - prefab:")[1:]
jobs, entries = [], []
for b in blocks:
    b = "\n    prefab:" + b
    g = re.search(r"prefab: \{fileID: \d+, guid: (\w+)", b)
    prefab_path = guid2path.get(g.group(1)) if g else None
    if not prefab_path:
        print("skip entry without prefab")
        continue
    model_id = os.path.splitext(os.path.basename(prefab_path))[0]
    category = CATEGORIES.get(int(field(b, "category") or 0), "Uncategorized")
    fbx = source_fbx(prefab_path)
    folder = os.path.dirname(fbx)
    info_path = os.path.join(folder, "info.json")
    info = json.load(io.open(info_path, encoding="utf-8")) if os.path.exists(info_path) else {}

    loai_index = int(field(b, "loai") or 0)
    offers = [{"storeName": yaml_str(s), "price": float(p)}
              for s, p in re.findall(r"- storeName: (.*)\n\s+price: ([-\d.e]+)", b)]
    texture = None
    if "Darth_Artisan" in fbx:
        texture = os.path.join(assets, "Assets", "Darth_Artisan", "Free_Trees", "Textures", "tree_diffuse.png")

    jobs.append({"id": model_id, "fbx": fbx, "texture": texture,
                 "out": os.path.join(out_dir, "models", category, model_id + ".glb")})
    entries.append({
        "id": model_id,
        "category": category,
        "displayName": yaml_str(field(b, "displayName")),
        "description": yaml_str(field(b, "description")),
        "listedPrice": float(field(b, "listedPrice") or 0),
        "offers": offers,
        "loai": info.get("loai") or (LOAI[loai_index] if loai_index < len(LOAI) else ""),
        "frontYawOffset": float(field(b, "frontYawOffset") or 0),
        "hasDoor": field(b, "hasDoor").strip() == "1",
        "doorOffset": vec(b, "doorOffset"),
        "baseSize": vec(b, "baseSize"),
        "modelUrl": "models/%s/%s.glb" % (category, model_id),
        "thumbnailUrl": "thumbnails/%s.png" % model_id,
        "author": info.get("author", "Darth_Artisan" if texture else ""),
        "license": info.get("license", "Unity Asset Store EULA" if texture else ""),
        "sourceUrl": info.get("url", ""),
        "_thumbnail": os.path.join(assets, "UI", "Thumbnails", model_id + ".png"),
    })

os.makedirs(work, exist_ok=True)
json.dump(jobs, io.open(os.path.join(work, "jobs.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
json.dump(entries, io.open(os.path.join(work, "entries.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print(len(jobs), "jobs")
