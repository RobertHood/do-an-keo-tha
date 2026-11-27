# Model library export (GLB for R2)

Turns the models in the Unity catalog (`Assets/Prefabs/PriceCatalog.asset`) into the folder that is uploaded to
the model storage (Cloudflare R2). The web build loads every model from there with glTFast, so the models are not
part of the Unity build.

Output (`ModelLibrary/` at the project root, upload its contents to the bucket root as is):

- `models/<Category>/<id>.glb`: one self-contained file per model (Draco-compressed mesh, textures at most 1024 px)
- `thumbnails/<id>.png`: catalog card pictures
- `catalog.json`: every model's data (name, price, phong thủy type, front direction, door, size, file URLs,
  license). The website sends it to Unity; the same fields are what a model database table should hold.

## Run

From this folder, with Blender 5.x (`E:/Blender/blender.exe`):

```sh
python make_jobs.py "E:/unity project/My project" "E:/unity project/My project/ModelLibrary" work
"E:/Blender/blender.exe" -b --factory-startup --python export_glb.py -- work/jobs.json work/results.json work/tmp
python write_catalog.py work "E:/unity project/My project/ModelLibrary"
```

`export_glb.py` checks every model: its size in Unity space must match the catalog's `baseSize`, and the GLB must
read back with the same bounds (see `work/results.json`). `render_glb.py` renders GLBs for a quick visual check:

```sh
"E:/Blender/blender.exe" -b --factory-startup --python render_glb.py -- <absolute out dir> a.glb b.glb
```

## Adding a model later

Import it into Unity as before (Tools > Suburbsim > Import Models, then Refresh Catalog for its thumbnail and
size), run the three commands, and upload the new GLB, thumbnail and catalog.json.

## Notes

- Axes: GLB from Blender + glTFast gives the same orientation as Unity's FBX import (Unity +Z = Blender -Y,
  Unity +X = Blender -X), so `frontYawOffset` and `doorOffset` stay valid.
- Materials: the FBX importer wires texture colour into alpha; the script removes that, keeps real alpha
  (leaves) as cut-out, and makes glass/water materials transparent.
