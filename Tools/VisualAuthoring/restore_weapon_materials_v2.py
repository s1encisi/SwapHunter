"""Restore the authored flat gun palettes only; preserve every GLB binary chunk."""
import bpy,sys,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from material_palette_bridge import read_flat_palette,restore_glb_flat_colors
names=["weapon_"+str(i).zfill(2) for i in range(9)]+["phase_blade"]
reports=[]
for name in names:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/"Tools/VisualAuthoring/Derived/WeaponsV2"/(name+".blend")))
    used={mat for obj in bpy.context.scene.objects if obj.type=="MESH" for mat in obj.data.materials if mat}
    palette=read_flat_palette(used)
    for material,value in palette.items():
        if "glass" in material.lower():
            value["rgbaLinear"][3]=.18
            value["adapterNote"]="Original RGB retained; alpha .18 is the documented FPS optical-visibility adaptation."
    glb=ROOT/"Game/Assets/_SwapHunter/Resources/VisualV2/Weapons"/(name+".glb")
    report=restore_glb_flat_colors(glb,palette);report["sourceBlend"]=name+".blend";report["palette"]=palette;reports.append(report)
path=ROOT/"Tools/VisualAuthoring/weapons-v2-material-report.json"
path.write_text(json.dumps({"kind":"Authored legacy flat palette restoration; no geometry/skin/animation/marker edits","reports":reports},ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print("WEAPON_PALETTES_RESTORED",len(reports))