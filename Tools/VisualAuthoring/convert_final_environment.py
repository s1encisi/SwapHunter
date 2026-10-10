"""Convert only selected, unchanged CC0 publisher meshes; run with Blender --disable-autoexec.
Original .blend files are never saved or overwritten. No publisher scripts/controllers are exported.
"""
import argparse,sys,json,struct
from pathlib import Path
import bpy
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from material_palette_bridge import read_flat_palette,restore_glb_flat_colors
p=argparse.ArgumentParser()
p.add_argument("--source",required=True);p.add_argument("--output",required=True);p.add_argument("--report",required=True)
args=p.parse_args(sys.argv[sys.argv.index("--")+1:])
if Path(args.report).exists():raise RuntimeError("Existing conversion evidence preserved")
src=Path(args.source);out=Path(args.output);out.mkdir(parents=True,exist_ok=True)
names=('Door_Single', 'Props_Base', 'Props_Capsule', 'Props_Chest', 'Props_Computer', 'Props_Crate', 'Props_Teleporter_1', 'Props_Laser', 'RoofTile_Empty', 'Props_Vessel_Tall')
report={"tool":"Blender "+bpy.app.version_string,"scope":"Selected CC0 static environment conversion; not Unity/native validation","assets":[]}
for name in names:
 destination=out/(name+".glb")
 if destination.exists():raise RuntimeError("Existing conversion preserved: "+str(destination))
 bpy.ops.wm.open_mainfile(filepath=str(src/(name+".blend")),load_ui=False,use_scripts=False)
 bpy.ops.object.select_all(action="DESELECT")
 meshes=[obj for obj in bpy.context.scene.objects if obj.type=="MESH"]
 if not meshes:raise RuntimeError("No mesh in "+name)
 points=[]
 for obj in meshes:
  obj.select_set(True)
  points.extend(obj.matrix_world@Vector(corner) for corner in obj.bound_box)
 minimum=[min(v[i] for v in points) for i in range(3)];maximum=[max(v[i] for v in points) for i in range(3)]
 bpy.ops.export_scene.gltf(filepath=str(destination),export_format="GLB",use_selection=True,
     export_yup=True,export_apply=True,export_animations=False,export_cameras=False,export_lights=False,export_materials="EXPORT")
 raw=destination.read_bytes();document=json.loads(raw[20:20+struct.unpack_from("<I",raw,12)[0]])
 usedMaterials=[bpy.data.materials[m["name"]] for m in document.get("materials",[])]
 colorRepair=restore_glb_flat_colors(destination,read_flat_palette(usedMaterials))
 raw=destination.read_bytes();length,kind=struct.unpack_from("<II",raw,12)
 data=json.loads(raw[20:20+length]);external=[item["uri"] for key in ("buffers","images") for item in data.get(key,[]) if "uri" in item and not item["uri"].startswith("data:")]
 if external:raise RuntimeError("Non-self-contained output: "+name)
 report["assets"].append({"name":name,"sourceMeshes":len(meshes),"sourceBoundsMin":minimum,"sourceBoundsMax":maximum,
     "sourceVertices":sum(len(obj.data.vertices) for obj in meshes),"exportMeshes":len(data.get("meshes",[])),
     "exportAnimations":len(data.get("animations",[])),"externalUris":external,"bytes":len(raw),"sourceColorRepair":colorRepair})
 print("CC0_ENV_EXPORTED="+name,flush=True)
Path(args.report).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
