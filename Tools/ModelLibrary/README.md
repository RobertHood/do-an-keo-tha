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

## House building materials

A house can list the building materials it is made of (glass, floor tiles, roof...). Unity then prices it as the
sum of quantity x unit price per material, each with its own store / custom unit price, instead of one listed price.
Quantities are at scale 1 and grow with the model's scale by unit (m² with scale², m³ with scale³, m with scale,
pieces stay the same).

1. Measure the render materials of the houses as a starting point:

   ```sh
   "E:/Blender/blender.exe" -b --factory-startup --python measure_materials.py -- work/measured.json "E:/unity project/My project/ModelLibrary/models/House/"*.glb
   ```

2. Write `materials.json` next to this script from those numbers (see the format in `write_catalog.py`). Map each
   render material to a real material, use one face of a pane / half of a two-sided wall, and drop decorations.
3. Run `write_catalog.py` again and upload `catalog.json`. A model database can hold the same list per house.

## Adding a model later

Import it into Unity as before (Tools > Suburbsim > Import Models, then Refresh Catalog for its thumbnail and
size), run the three commands, and upload the new GLB, thumbnail and catalog.json.

## Notes

- Axes: GLB from Blender + glTFast gives the same orientation as Unity's FBX import (Unity +Z = Blender -Y,
  Unity +X = Blender -X), so `frontYawOffset` and `doorOffset` stay valid.
- Materials: the FBX importer wires texture colour into alpha; the script removes that, keeps real alpha
  (leaves) as cut-out, and makes glass/water materials transparent.
