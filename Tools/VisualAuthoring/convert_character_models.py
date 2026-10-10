
"""Export actual new CC0 character bodies, skinned FPS arms and George, preserving source files.
Run in Blender 5.2.2 with --disable-autoexec. No publisher scripts/controllers are exported.
"""
import argparse,sys,json,struct,math,bmesh
from pathlib import Path
import bpy
from mathutils import Vector,Matrix
sys.path.insert(0,str(Path(__file__).resolve().parent))
from material_palette_bridge import read_flat_palette,restore_glb_flat_colors
parser=argparse.ArgumentParser()
parser.add_argument("--project",required=True);parser.add_argument("--output",required=True);parser.add_argument("--report",required=True)
parser.add_argument("--only",default="")
args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
project=Path(args.project);out=Path(args.output);reportPath=Path(args.report)
if reportPath.exists():raise RuntimeError("Existing export report preserved")
out.mkdir(parents=True,exist_ok=True)
sources=project/"Tools/VisualAuthoring/Sources"
jobs=[("class_00","BlueSoldier_Male",False),("class_01","Soldier_Male",False),("class_02","Ninja_Male",False),
      ("class_03","Worker_Male",False),("enemy_assault","BlueSoldier_Male",False),("enemy_sniper","Ninja_Male",False),
      ("enemy_shield","Soldier_Male",False),("boss_george","George",False),("fps_arms","BlueSoldier_Male",True)]
if args.only:jobs=[j for j in jobs if j[0] in args.only.split(",")]
report={"schemaVersion":1,"blender":bpy.app.version_string,"axes":"Metres; glTF/Unity Y-up and visually +Z-forward. Instantiate directly; do not apply old project -X coordinateCorrection.","models":[]}
def skinned_meshes(rig):
 return [o for o in bpy.context.scene.objects if o.type=="MESH" and any(m.type=="ARMATURE" and m.object==rig for m in o.modifiers)]
def mesh_bounds(meshes):
 points=[];deps=bpy.context.evaluated_depsgraph_get()
 for obj in meshes:
  evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh()
  points.extend(evaluated.matrix_world@v.co for v in mesh.vertices);evaluated.to_mesh_clear()
 if not points:raise RuntimeError("No skinned geometry")
 return Vector([min(v[i] for v in points) for i in range(3)]),Vector([max(v[i] for v in points) for i in range(3)])
def regions(meshes):
 result={};deps=bpy.context.evaluated_depsgraph_get()
 groups={"head":{"Head"},"torso":{"Body","Hips","Abdomen","Torso","Chest"},
         "feet":{"Foot.L","Foot.R","FootBack.L","FootBack.R","Toes.L","Toes.R"}}
 for key,names in groups.items():
  points=[]
  for obj in meshes:
   indexes={group.index for group in obj.vertex_groups if group.name in names}
   evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh()
   if len(mesh.vertices)!=len(obj.data.vertices):raise RuntimeError("Region weights need matching evaluated topology")
   for vertex in obj.data.vertices:
    if sum(weight.weight for weight in vertex.groups if weight.group in indexes)>.35:
     v=evaluated.matrix_world@mesh.vertices[vertex.index].co
     points.append(Vector((-v.x,v.z,-v.y)))
   evaluated.to_mesh_clear()
  if points:
   low=[min(v[i] for v in points) for i in range(3)];high=[max(v[i] for v in points) for i in range(3)]
   result[key]={"min":low,"max":high,"size":[high[i]-low[i] for i in range(3)],"influencedVertices":len(points),"summedWeightThreshold":.35}
 return result
def activate(rig,action):
 rig.animation_data_create()
 for track in rig.animation_data.nla_tracks:track.mute=True
 rig.animation_data.action=action
 if action.slots:rig.animation_data.action_slot=action.slots[0]
 bpy.context.scene.frame_set(int(action.frame_range[0]));bpy.context.view_layer.update()
def marker(rig,name,bone_name,head=True):
 bone=rig.pose.bones[bone_name]
 point=bone.head if head else (bone.head+bone.tail)*.5
 obj=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(obj)
 obj.parent=rig;obj.parent_type="BONE";obj.parent_bone=bone_name
 obj.matrix_world=Matrix.Translation(rig.matrix_world@point)
 bpy.context.view_layer.update();return obj
def parse_glb(path):
 raw=path.read_bytes();length,typ=struct.unpack_from("<II",raw,12);data=json.loads(raw[20:20+length])
 at=20+length
 binlen,bintype=struct.unpack_from("<II",raw,at);return data,raw[at+8:at+8+binlen]
def accessor(data,binary,index):
 a=data["accessors"][index];v=data["bufferViews"][a["bufferView"]]
 sizes={"SCALAR":1,"VEC2":2,"VEC3":3,"VEC4":4,"MAT4":16};n=sizes[a["type"]]
 formats={5126:"f",5125:"I",5123:"H",5121:"B"};fmt=formats[a["componentType"]];unit=struct.calcsize("<"+fmt)*n
 start=v.get("byteOffset",0)+a.get("byteOffset",0);stride=v.get("byteStride",unit)
 return [list(struct.unpack_from("<"+fmt*n,binary,start+i*stride)) for i in range(a["count"])]
def write_glb(path,data,binary):
 text=json.dumps(data,separators=(",",":"),ensure_ascii=True).encode("utf-8");text+=b" "*((-len(text))%4)
 binary+=b"\0"*((-len(binary))%4)
 payload=struct.pack("<II",len(text),0x4e4f534a)+text+struct.pack("<II",len(binary),0x004e4942)+binary
 path.write_bytes(struct.pack("<III",0x46546c67,2,len(payload)+12)+payload)
def keep_idle_default(data,binary):
 idle=next((a for a in data.get("animations",[]) if a.get("name","").split("|")[-1].lower()=="idle"),None)
 if not idle:return
 for channel in idle["channels"]:
  target=channel["target"];field=target.get("path")
  if field not in ("translation","rotation","scale"):continue
  sampler=idle["samplers"][channel["sampler"]];values=accessor(data,binary,sampler["output"])
  value=values[1 if sampler.get("interpolation")=="CUBICSPLINE" else 0]
  node=data["nodes"][target["node"]];node.pop("matrix",None);node[field]=value
def world_matrices(data):
 cache={};parents={c:i for i,n in enumerate(data["nodes"]) for c in n.get("children",[])}
 from mathutils import Quaternion
 def matrix(i):
  if i in cache:return cache[i]
  n=data["nodes"][i]
  if "matrix" in n:local=Matrix([n["matrix"][j:j+4] for j in range(0,16,4)]).transposed()
  else:
   q=n.get("rotation",[0,0,0,1]);local=Matrix.LocRotScale(Vector(n.get("translation",[0,0,0])),Quaternion((q[3],q[0],q[1],q[2])),Vector(n.get("scale",[1,1,1])))
  cache[i]=matrix(parents[i])@local if i in parents else local;return cache[i]
 for i in range(len(data["nodes"])):matrix(i)
 return cache,parents
def metadata(data,binary,record):
 matrices,parents=world_matrices(data)
 paths={}
 for i,node in enumerate(data["nodes"]):
  if not node.get("name"):continue
  parts=[];j=i
  while True:
   parts.append(data["nodes"][j].get("name","Node_"+str(j)))
   if j not in parents:break
   j=parents[j]
  paths[node["name"]]="/".join(reversed(parts))
 names={n.get("name"):i for i,n in enumerate(data["nodes"])}
 wanted=["VisualV2Root","CharacterArmature","RobotArmature","Bone","Body","Hips","Abdomen","Torso","Chest","Neck","Head",
 "Shoulder.L","UpperArm.L","LowerArm.L","Fist.L","Shoulder.R","UpperArm.R","LowerArm.R","Fist.R",
 "PalmI.L","PalmI.R","UpperLeg.L","MidLeg.L","LowerLeg.L","Foot.L","FootBack.L","UpperLeg.R","MidLeg.R","LowerLeg.R","Foot.R","FootBack.R",
 "hand_L","forearm_L","hand_R","forearm_R","wrist_L","wrist_R","elbow_L","elbow_R","PalmContact_L","PalmContact_R","WeaponMount_R","WeaponMount_L"]
 record["nodePaths"]={n:paths[n] for n in wanted if n in paths}
 record["nodeWorldPositionsGltf"]={n:list(matrices[names[n]].translation) for n in wanted if n in names}
 record["nodeWorldPositionsUnity"]={n:[-v[0],v[1],v[2]] for n,v in record["nodeWorldPositionsGltf"].items()}
 record["clips"]=[]
 for anim in data.get("animations",[]):
  starts=[];ends=[]
  for s in anim["samplers"]:
   times=accessor(data,binary,s["input"]);starts.append(times[0][0]);ends.append(times[-1][0])
  record["clips"].append({"name":anim.get("name"),"seconds":max(ends)-min(starts)})
 record["meshes"]=len(data.get("meshes",[]));record["skins"]=len(data.get("skins",[]))
 record["vertexCounts"]=[data["accessors"][p["attributes"]["POSITION"]]["count"] for mesh in data.get("meshes",[]) for p in mesh["primitives"]]
 record["externalUris"]=[i["uri"] for k in ("buffers","images") for i in data.get(k,[]) if "uri" in i and not i["uri"].startswith("data:")]
 if record["externalUris"]:raise RuntimeError("Export has external URI")
 return record
for output_name,original,fp in jobs:
 boss=original=="George";src=sources/("AnimatedMech/George.gltf" if boss else "AnimatedCharacters2019/"+original+".blend")
 destination=out/("Characters" if fp else "Actors")/(output_name+".glb");destination.parent.mkdir(parents=True,exist_ok=True)
 if destination.exists():raise RuntimeError("Existing generated model preserved: "+str(destination))
 if boss:
  bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(src))
 else:bpy.ops.wm.open_mainfile(filepath=str(src),load_ui=False,use_scripts=False)
 if bpy.context.object and bpy.context.object.mode!="OBJECT":bpy.ops.object.mode_set(mode="OBJECT")
 rigs=[o for o in bpy.context.scene.objects if o.type=="ARMATURE"]
 if len(rigs)!=1:raise RuntimeError("Expected one real character rig: "+original)
 rig=rigs[0];meshes=skinned_meshes(rig)
 for meshIndex,obj in enumerate(meshes):obj.name="VisualMesh_"+str(meshIndex)
 palette=read_flat_palette({mat for obj in meshes for mat in obj.data.materials if mat})
 idle=next(a for a in bpy.data.actions if a.name.lower()=="idle");activate(rig,idle)
 low,high=mesh_bounds(meshes);height=2.1 if boss else 1.8;uniform=height/(high.z-low.z)
 scale=Vector((uniform,uniform,uniform))
 if not fp:
  scale.x=min(uniform,(1.05 if boss else .72)/(high.x-low.x))
  scale.y=min(uniform,(.90 if boss else .72)/(high.y-low.y))
 feet=[rig.matrix_world@rig.pose.bones[name].head for name in ("Foot.L","Foot.R") if name in rig.pose.bones]
 center=(sum(feet,Vector())/len(feet)) if feet else (low+high)*.5
 offset=Vector((-center.x*scale.x,-center.y*scale.y,-low.z*scale.z))
 record={"id":output_name,"source":str(src.relative_to(project)),"sourceActionNames":[a.name for a in bpy.data.actions],
 "scope":"New CC0 source mesh and rig; no old project body/arms geometry","sourceIdleBoundsBlender":{"min":list(low),"max":list(high)},
 "targetHeight":height,"normalizationScaleBlender":list(scale),"firstPersonArms":fp}
 markers=[]
 if fp:
  # Keep only vertices driven primarily by the new source's arm chain; no head/torso/legs remain.
  kept=0;removed=0
  allowed={b.name for b in rig.data.bones if b.name.startswith(("Shoulder.","UpperArm.","LowerArm.","Fist."))}
  for obj in meshes:
   armgroups={g.index for g in obj.vertex_groups if g.name in allowed}
   selected={v.index for v in obj.data.vertices if sum(g.weight for g in v.groups if g.group in armgroups)>=.55}
   bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table()
   discard=[v for v in bm.verts if v.index not in selected];removed+=len(discard);kept+=len(bm.verts)-len(discard)
   bmesh.ops.delete(bm,geom=discard,context="VERTS");bm.to_mesh(obj.data);bm.free();obj.data.update()
  if kept<50 or removed<50:raise RuntimeError("Arm extraction failed")
  record["armExtraction"]={"keptVertices":kept,"removedVertices":removed,"weightThreshold":.55}
  rig.animation_data_clear();rig.data.pose_position="REST";bpy.context.view_layer.update()
  for old,new in {"LowerArm.L":"forearm_L","Fist.L":"hand_L","LowerArm.R":"forearm_R","Fist.R":"hand_R"}.items():
   rig.data.bones[old].name=new
   for obj in meshes:
    group=obj.vertex_groups.get(old)
    if group:group.name=new
  bpy.context.view_layer.update()
  for side in ("L","R"):
   markers.extend([marker(rig,"wrist_"+side,"hand_"+side),marker(rig,"elbow_"+side,"forearm_"+side),
                   marker(rig,"PalmContact_"+side,"hand_"+side,False)])
  shoulders=[rig.matrix_world@rig.data.bones["UpperArm."+s].head_local for s in ("L","R")]
  mean=sum(shoulders,Vector())/2
  offset=Vector((-mean.x*scale.x,.30-mean.y*scale.y,-.08-mean.z*scale.z))
  record["fpNotes"]="Full new skeleton retained for skinning; only arm mesh exported. Real hand/forearm bones renamed. PalmContact is midpoint of new Fist bone; wrist is bone head. Hierarchical IK required, not old sibling-object IK."
 else:
  for side in ("L","R"):
   hand=("PalmI." if boss else "Fist.")+side
   markers.append(marker(rig,"WeaponMount_"+side,hand))
 wrapper=bpy.data.objects.new("VisualV2Root",None);bpy.context.collection.objects.link(wrapper)
 selected=[rig]+meshes+markers
 for obj in selected:
  if obj.parent not in selected:
   matrix=obj.matrix_world.copy();obj.parent=wrapper;obj.matrix_world=matrix
 wrapper.scale=scale;wrapper.location=offset;bpy.context.view_layer.update()
 scaledLow,scaledHigh=mesh_bounds(meshes)
 record["normalizedReferenceBoundsBlender"]={"min":list(scaledLow),"max":list(scaledHigh)}
 record["normalizedReferenceBoundsUnity"]={"min":[-scaledHigh.x,scaledLow.z,-scaledHigh.y],"max":[-scaledLow.x,scaledHigh.z,-scaledLow.y]}
 if not fp:record["boneRegionBoundsUnity"]=regions(meshes)
 record["regionBasis"]="Blender evaluated normalized Idle, vertices with summed named-bone influence > .35; not a collision acceptance test"
 bpy.ops.object.select_all(action="DESELECT")
 for obj in [wrapper]+selected:obj.select_set(True)
 bpy.context.view_layer.objects.active=rig
 options=dict(filepath=str(destination),export_format="GLB",use_selection=True,export_yup=True,export_apply=False,
  export_cameras=False,export_lights=False,export_materials="EXPORT",export_animations=not fp,export_skins=True,
  export_animation_mode="ACTIONS",export_force_sampling=True,export_anim_single_armature=True,export_optimize_animation_size=True)
 bpy.ops.export_scene.gltf(**options)
 data,binary=parse_glb(destination)
 if not fp:keep_idle_default(data,binary);write_glb(destination,data,binary)
 record["authoredMaterialPalette"]=palette
 record["materialBridge"]=restore_glb_flat_colors(destination,palette)
 data,binary=parse_glb(destination)
 metadata(data,binary,record)
 if not fp and len(record["clips"])<(20 if boss else 17):raise RuntimeError("Animations lost during export: "+output_name)
 report["models"].append(record);print("NEW_MODEL_EXPORTED="+output_name+";clips="+str(len(record["clips"])),flush=True)
reportPath.parent.mkdir(parents=True,exist_ok=True);reportPath.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print("CHARACTER_EXPORT_COMPLETE",flush=True)

