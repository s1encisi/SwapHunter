"""Author new CC0 weapon rigs. Run with Blender --background --disable-autoexec.
Original files are never modified. Exported root carries the glTF-to-Unity X correction,
so runtime wrappers use scale one and never reuse ModelLibrary's legacy correction.
"""
import argparse, json, math, hashlib, sys
from pathlib import Path
from collections import Counter
import bpy
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from material_palette_bridge import read_flat_palette,restore_glb_flat_colors

ROOT=Path(__file__).resolve().parents[2]
QUAT=ROOT/"Tools/VisualAuthoring/Sources/QuaterniusUltimateGuns"
KENNEY=ROOT/"Tools/VisualAuthoring/Sources/KenneyBlasterKit"
OUT=ROOT/"Game/Assets/_SwapHunter/Resources/VisualV2/Weapons"
DERIVED=ROOT/"Tools/VisualAuthoring/Derived/WeaponsV2"
PROFILES=[
 dict(id=0,file="AssaultRifle_1",length=.84,grip=(.08,0,.03),support=(1.32,-.07,.30),mag=(.82,1.18,-.44),label="Carbine"),
 dict(id=1,file="Shotgun_2",length=1.02,grip=(.28,0,-.10),support=(1.77,-.08,-.05),label="Pump"),
 dict(id=2,file="Bullpup_2",length=.80,grip=(0,0,.02),support=(1.0,-.10,.38),mag=(-.86,-.40,-.34),label="Burst bullpup"),
 dict(id=3,file="AssaultRifle2_3",length=.92,grip=(0,0,.02),support=(1.0,-.10,.37),mag=(.49,.94,-.42),label="Heavy rifle"),
 dict(id=4,file="SubmachineGun_1",length=.62,grip=(0,0,-.04),support=(1.35,-.13,.51),mag=(.69,1.12,-.76),label="SMG"),
 dict(id=5,file="Shotgun_SawedOff",length=.66,grip=(.05,0,-.18),support=(1.80,-.14,.12),label="Double barrel"),
 dict(id=6,file="SniperRifle_2",length=1.10,grip=(.30,0,-.18),support=(1.72,-.06,.10),mag=(1.34,1.81,-.16),label="DMR"),
 dict(id=7,file="SniperRifle_5",length=1.18,grip=(-.85,0,-.03),support=(.90,-.08,.23),label="Bolt sniper"),
 dict(id=8,file="AssaultRifle2_4",length=1.00,grip=(0,0,.02),support=(.95,-.10,.42),mag=(.63,1.14,-.51),label="Support LMG"),
 dict(id=9,file="blaster-m",length=.72,grip=(0,-.18,-.105),support=(0,.02,-.09),label="Grenade launcher",kenney=True),
 dict(id=10,file="blaster-g",length=.90,grip=(0,-.23,-.10),support=(0,-.04,-.07),label="Coil rifle",kenney=True),
 dict(id=11,file="blaster-r",length=.78,grip=(0,-.15,-.10),support=(0,.01,-.07),label="Phase marker",kenney=True),
]
def B(v):return Vector((v[0],-v[2],v[1]))
def empty(name,u=(0,0,0),parent=None):
 o=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(o);o.location=B(u);o.parent=parent;return o
def material(name,color,metal=.0,emission=0):
 m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=color
 p=m.node_tree.nodes.get("Principled BSDF");p.inputs["Base Color"].default_value=color
 p.inputs["Metallic"].default_value=metal;p.inputs["Roughness"].default_value=.48
 if emission:p.inputs["Emission Color"].default_value=color;p.inputs["Emission Strength"].default_value=emission
 return m
def preserve_materials():
 for m in bpy.data.materials:
  if not m.use_nodes:
   color=tuple(m.diffuse_color);m.use_nodes=True;m.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value=color
  if "glass" in m.name.lower():
   bsdf=m.node_tree.nodes.get("Principled BSDF")
   if bsdf:bsdf.inputs["Alpha"].default_value=.18
   if hasattr(m,"surface_render_method"):m.surface_render_method="DITHERED"
def load(profile):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 if profile.get("kenney"):
  bpy.ops.import_scene.gltf(filepath=str(KENNEY/(profile["file"]+".glb")))
 else:
  with bpy.data.libraries.load(str(QUAT/(profile["file"]+".blend")),link=False) as (a,b):b.objects=list(a.objects)
  for o in b.objects:
   if o:bpy.context.scene.collection.objects.link(o)
 bpy.context.view_layer.update();preserve_materials()
 return [o for o in bpy.context.scene.objects if o.type=="MESH"]
def components(mesh):
 adjacent=[set() for _ in mesh.vertices]
 for edge in mesh.edges:
  a,b=edge.vertices;adjacent[a].add(b);adjacent[b].add(a)
 remaining=set(range(len(adjacent)));result=[]
 while remaining:
  pending=[remaining.pop()];part=set(pending)
  while pending:
   for index in adjacent[pending.pop()]:
    if index in remaining:remaining.remove(index);part.add(index);pending.append(index)
  result.append(part)
 return result
def mesh_piece(source,faces,convert,name,parent,origin=(0,0,0)):
 mesh=source.data;used=sorted({v for face in faces for v in face.vertices});mapping={v:i for i,v in enumerate(used)}
 data=bpy.data.meshes.new(name+"_mesh")
 vertices=[B(convert(source.matrix_world@mesh.vertices[v].co)-Vector(origin)) for v in used]
 data.from_pydata(vertices,[],[[mapping[v] for v in p.vertices] for p in faces]);data.update()
 for m in mesh.materials:data.materials.append(m)
 if mesh.uv_layers:
  for old_uv in mesh.uv_layers:
   uv=data.uv_layers.new(name=old_uv.name)
   for old_face,new_face in zip(faces,data.polygons):
    for old_loop,new_loop in zip(old_face.loop_indices,new_face.loop_indices):uv.data[new_loop].uv=old_uv.data[old_loop].uv
 for old,new in zip(faces,data.polygons):new.material_index=old.material_index;new.use_smooth=old.use_smooth
 o=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(o);o.parent=parent
 return o
def cube(name,center,size,parent,mat):
 bpy.ops.mesh.primitive_cube_add(size=1,location=B(center));o=bpy.context.object;o.name=name
 o.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 o.parent=parent;o.data.materials.append(mat);return o
def cylinder(name,center,radius,length,parent,mat,axis="forward",vertices=12):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=length,location=B(center))
 o=bpy.context.object;o.name=name
 if axis=="forward":o.rotation_euler=(math.pi/2,0,0)
 elif axis=="right":o.rotation_euler=(0,math.pi/2,0)
 o.parent=parent;o.data.materials.append(mat);return o
def selected_objects(root):
 bpy.ops.object.select_all(action="DESELECT");root.select_set(True)
 for o in root.children_recursive:o.select_set(True)
 bpy.context.view_layer.objects.active=root
def bbox(root):
 pts=[o.matrix_world@Vector(c) for o in root.children_recursive if o.type=="MESH" for c in o.bound_box]
 return Vector(tuple(min(v[i] for v in pts) for i in range(3))),Vector(tuple(max(v[i] for v in pts) for i in range(3)))
def make_profile(profile):
 imported=load(profile);all_points=[o.matrix_world@v.co for o in imported for v in o.data.vertices]
 forward_axis=1 if profile.get("kenney") else 0
 lo=min(v[forward_axis] for v in all_points);hi=max(v[forward_axis] for v in all_points)
 scale=profile["length"]/(hi-lo);g=Vector(profile["grip"])
 def convert(v):
  if profile.get("kenney"):return Vector(((v.x-g.x)*scale,(v.z-g.z)*scale-.10,(v.y-g.y)*scale-.10))
  return Vector(((v.y-g.y)*scale,(v.z-g.z)*scale-.10,(v.x-g.x)*scale-.10))
 root=empty("CC0_weapon_"+str(profile["id"]).zfill(2));root.scale=(-1,1,1)
 root["sourceAsset"]=profile["file"];root["weaponId"]=profile["id"];root["axes"]="Unity +Y up +Z forward; authored glTF X reflection"
 body=empty("receiver",parent=root)
 dark=material("Project action metal",(.09,.115,.13,1),.72)
 brass=material("Project cartridge brass",(.52,.32,.085,1),.55)
 glow=material("Project phase energy",(.04,.65,.82,1),.15,2.5)
 grey=material("Project feed cover",(.20,.235,.25,1),.65)
 mag_origin=Vector((0,-.04,.08));mag=empty("magazine",mag_origin,root)
 bolt_origin=Vector((.045,.035,.015));bolt=empty("charging_handle",bolt_origin,root)
 pump_origin=convert(Vector((1.86,0,.04))) if profile["id"]==1 else Vector((0,-.03,.21))
 pump=empty("foregrip",pump_origin,root) if profile["id"]==1 else None
 hinge_origin=convert(Vector((.78,0,.30))) if profile["id"]==5 else Vector((0,.005,.10))
 hinge_name={5:"break_action",8:"feed_cover",9:"cylinder_hinge"}.get(profile["id"])
 hinge=empty(hinge_name,hinge_origin,root) if hinge_name else None
 mag_parts=0;bolt_parts=0;hinge_parts=0;pump_parts=0
 for source in imported:
  parts=components(source.data)
  for number,part in enumerate(parts):
   faces=[p for p in source.data.polygons if p.vertices[0] in part]
   verts=[source.matrix_world@source.data.vertices[v].co for v in part]
   minimum=Vector(tuple(min(v[i] for v in verts) for i in range(3)));maximum=Vector(tuple(max(v[i] for v in verts) for i in range(3)))
   center=(minimum+maximum)/2;target=body;origin=Vector((0,0,0));part_name="source_"+source.name+"_"+str(number)
   is_mag=False
   if profile.get("kenney"):is_mag="magazine" in source.name.lower()
   elif "mag" in profile:
    a,b,z=profile["mag"];is_mag=minimum.x>=a-.02 and maximum.x<=b+.02 and minimum.z<z+.05 and maximum.z>0
   if is_mag:
    if profile["id"]==8:continue
    target=mag;mag_parts+=1
    # Use the real magazine's centre as its stable rigid-part pivot.
    if mag_parts==1:
     mag_origin=convert(center);mag.location=B(mag_origin)
    origin=mag_origin;part_name="source_magazine_"+str(number)
   elif profile["id"]==1 and minimum.x>1.30 and maximum.x<2.41 and minimum.z<-.08 and maximum.z<.20:
    target=pump;origin=pump_origin;pump_parts+=1;part_name="source_pump"
   elif profile["id"]==5 and minimum.x>=.76:
    target=hinge;origin=hinge_origin;hinge_parts+=1;part_name="source_break_barrel_"+str(number)
   elif profile["id"]==7 and minimum.y<-.28 and maximum.x-minimum.x<.09:
    target=bolt;bolt_origin=convert(center);bolt.location=B(bolt_origin);origin=bolt_origin;bolt_parts+=1;part_name="source_bolt_handle"
   elif profile["id"]==9 and center.y>.03:
    target=hinge;origin=hinge_origin;hinge_parts+=1;part_name="source_launcher_cluster_"+str(number)
   mesh_piece(source,faces,convert,part_name,target,origin)
 for o in imported:bpy.data.objects.remove(o,do_unlink=True)
 if not bolt_parts:
  cylinder("action_stem",(0,0,0),.009,.065,bolt,dark,"right")
  cube("action_latch",(.03,0,0),(.027,.018,.020),bolt,dark)
 if not mag_parts:
  if profile["id"] in (1,5,7):
   if profile["id"]==5:
    mag.parent=hinge;mag.location=B((0,0,.05));mag_origin=hinge_origin+Vector((0,0,.05))
   rounds=empty("reload_rounds",parent=mag)
   count=2 if profile["id"]!=7 else 5
   for i in range(count):
    pos=((i-(count-1)/2)*.021,0,0)
    cylinder("load_round_"+str(i),pos,.009,.062,rounds,brass)
   mag_parts=count
  elif profile["id"]==9:
   mag.parent=hinge;mag.location=B((0,0,.02));mag_origin=hinge_origin+Vector((0,0,.02))
   rounds=empty("reload_rounds",parent=mag)
   for i in range(4):
    pos=((i%2-.5)*.07,(i//2-.5)*.07,0)
    cylinder("grenade_round_"+str(i),pos,.026,.10,rounds,brass,vertices=16)
   mag_parts=4
  else:
   cube("removable_feed_box",(0,-.045,0),(.105,.14,.105) if profile["id"]==8 else (.057,.13,.07),mag,dark)
   cube("feed_box_lip",(0,.022,0),(.112,.025,.10) if profile["id"]==8 else (.06,.025,.072),mag,grey);mag_parts=2
 if profile["id"]==8:
  cube("hinged_feed_cover",(0,.04,.03),(.13,.032,.20),hinge,grey)
  belt=empty("ammo_belt",(.07,.005,.085),root)
  for i in range(7):cylinder("belt_round_"+str(i),(i*.016,-abs(i-2)*.002,0),.006,.065,belt,brass)
  mount_y=.09
  cube("bipod_mount",(0,mount_y,.36),(.16,.035,.045),root,dark)
  for side in (-1,1):
   leg=cube("support_bipod_"+str(side),(side*.075,mount_y-.125,.36),(.018,.25,.018),root,dark)
   leg.rotation_euler=(0,-side*.25,0)
 if profile["id"]==10:
  for i in range(5):
   cylinder("charging_coil_"+str(i),(0,.035,.17+i*.057),.060,.018,root,glow,vertices=16)
  cylinder("coil_core",(0,.035,.29),.026,.32,root,dark,vertices=16)
 if profile["id"]==11:
  cube("phase_emitter",(0,.045,.27),(.13,.065,.035),root,glow)
  cube("phase_sensor_left",(-.08,.055,.20),(.035,.04,.10),root,dark)
  cube("phase_sensor_right",(.08,.055,.20),(.035,.04,.10),root,dark)
 # Mark the actual muzzle from frontmost source vertices; optics use the true upper sight line.
 front=[v for v in all_points if v[forward_axis]>hi-(hi-lo)*.006]
 tip=convert(sum(front,Vector())/len(front));tip.x=0;tip.z+=.006
 if profile["id"]==8:
  cylinder("heavy_extended_barrel",(0,tip.y,tip.z+.07),.021,.16,root,dark,vertices=16);tip.z+=.15
 muzzle=empty("Muzzle",tip,root)
 empty("Grip",(0,-.10,-.10),root)
 support=convert(Vector(profile["support"]));empty("SupportGrip",support,root)
 if profile["id"] in (6,7):
  # Imported optical tube is aligned to these measured source lens-centre heights.
  optic_z=.556 if profile["id"]==6 else .794
  sight_y=convert(Vector((0,0,optic_z))).y
 else:
  sight_y=max(convert(v).y for v in all_points)+.01
  rear_base=min(0,sight_y-.025);front_base=min(tip.y,sight_y-.02)
  cube("rear_sight_post",(0,(sight_y+rear_base)*.5,-.055),(.014,sight_y-rear_base,.018),root,dark)
  cube("front_sight_post",(0,(sight_y+front_base)*.5,max(.12,tip.z-.045)),(.012,sight_y-front_base,.014),root,dark)
 empty("AimRear",(0,sight_y,-.055),root);empty("AimFront",(0,sight_y,max(.12,tip.z-.045)),root)
 # A marker on a real magazine surface drives hand contact; no old weapon-4 coordinate override.
 empty("MagazineGrip",(-.06 if profile["id"]==8 else -.034,-.018,0),mag)
 empty("MagazineExit",(0,-1,0),mag)
 bpy.context.view_layer.update()
 for parent in [o for o in root.children_recursive if o.type=="EMPTY"]+[root]:
  group=[o for o in parent.children if o.type=="MESH"]
  if len(group)>1:
   bpy.ops.object.select_all(action="DESELECT")
   for o in group:o.select_set(True)
   bpy.context.view_layer.objects.active=group[0]
   bpy.ops.object.join();group[0].name=parent.name+"_geometry"
 for image in bpy.data.images:
  if image.source=="FILE" and not image.packed_file:
   try:image.pack()
   except RuntimeError:pass
 for o in list(bpy.context.scene.objects):
  if o!=root and o not in root.children_recursive:bpy.data.objects.remove(o,do_unlink=True)
 return root,{"id":profile["id"],"label":profile["label"],"source":profile["file"],"sourceFamily":"Kenney Blaster Kit 2.1" if profile.get("kenney") else "Quaternius Ultimate Gun Pack",
              "realMagazineParts":mag_parts,"realBoltParts":bolt_parts,"realHingeParts":hinge_parts,"realPumpParts":pump_parts,
              "muzzleUnity":list(tip),"gripUnity":[0,-.10,-.10],"supportGripUnity":list(support),"sightYUnity":sight_y}
def export(root,path):
 selected_objects(root)
 bpy.ops.export_scene.gltf(filepath=str(path),export_format="GLB",use_selection=True,export_yup=True,export_animations=False,export_extras=True)
 used={m for o in root.children_recursive if o.type=="MESH" for m in o.data.materials if m}
 textured=any(m.use_nodes and m.node_tree and any(n.type=="TEX_IMAGE" for n in m.node_tree.nodes) for m in used)
 if not textured:
  palette=read_flat_palette(used)
  for name,value in palette.items():
   if "glass" in name.lower():value["rgbaLinear"][3]=.18
  restore_glb_flat_colors(path,palette)
def render(root,path):
 mn,mx=bbox(root);center=(mn+mx)/2
 world=bpy.data.worlds.new("Inspection world");bpy.context.scene.world=world;world.use_nodes=True
 world.node_tree.nodes["Background"].inputs[0].default_value=(.16,.18,.21,1);world.node_tree.nodes["Background"].inputs[1].default_value=.45
 for location,energy,size in (((-2,-3,4),600,4),((2,1,2),350,3)):
  light=bpy.data.lights.new("Inspection softbox","AREA");light.energy=energy;light.shape="DISK";light.size=size
  obj=bpy.data.objects.new("Inspection softbox",light);bpy.context.scene.collection.objects.link(obj);obj.location=center+Vector(location);obj.rotation_euler=(center-obj.location).to_track_quat("-Z","Y").to_euler()
 cam_data=bpy.data.cameras.new("Inspection camera");camera=bpy.data.objects.new("Inspection camera",cam_data);bpy.context.scene.collection.objects.link(camera)
 camera.location=center+Vector((-2.2,-2.0,1.25));camera.rotation_euler=(center-camera.location).to_track_quat("-Z","Y").to_euler()
 cam_data.type="ORTHO";cam_data.ortho_scale=max(1.12,(mx-mn).length*1.12);bpy.context.scene.camera=camera
 scene=bpy.context.scene;scene.render.engine="CYCLES";scene.cycles.samples=12;scene.cycles.use_denoising=True
 scene.render.resolution_x=520;scene.render.resolution_y=300;scene.render.resolution_percentage=100
 scene.render.image_settings.file_format="PNG";scene.render.filepath=str(path);scene.view_settings.view_transform="Standard"
 bpy.ops.render.render(write_still=True)
def blade():
 imported=load(dict(file="Accessories/Bayonet"))
 root=empty("CC0_phase_blade");root.scale=(-1,1,1)
 pts=[o.matrix_world@v.co for o in imported for v in o.data.vertices];scale=.5/(max(v.x for v in pts)-min(v.x for v in pts))
 def convert(v):return Vector((v.y*scale,v.z*scale,(v.x)*scale-.105))
 for o in imported:mesh_piece(o,list(o.data.polygons),convert,"source_bayonet",root)
 for o in imported:bpy.data.objects.remove(o,do_unlink=True)
 empty("Grip",(0,0,-.105),root);empty("BladeTip",(0,0,max(convert(v).z for v in pts)),root)
 return root
def main():
 parser=argparse.ArgumentParser();parser.add_argument("--preview-dir",type=Path,required=True);parser.add_argument("--replace-derived",action="store_true");args=parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
 OUT.mkdir(parents=True,exist_ok=True);DERIVED.mkdir(parents=True,exist_ok=True);args.preview_dir.mkdir(parents=True,exist_ok=True)
 rows=[]
 for profile in PROFILES:
  destination=OUT/("weapon_"+str(profile["id"]).zfill(2)+".glb")
  if destination.exists() and not args.replace_derived:raise RuntimeError("Refusing to overwrite existing export: "+str(destination))
  root,row=make_profile(profile);export(root,destination)
  bpy.ops.wm.save_as_mainfile(filepath=str(DERIVED/(destination.stem+".blend")))
  render(root,args.preview_dir/(destination.stem+".png"))
  row["resourcePath"]="VisualV2/Weapons/"+destination.stem;row["glbSha256"]=hashlib.sha256(destination.read_bytes()).hexdigest();rows.append(row)
 root=blade();export(root,OUT/"phase_blade.glb");bpy.ops.wm.save_as_mainfile(filepath=str(DERIVED/"phase_blade.blend"))
 render(root,args.preview_dir/"phase_blade.png")
 report={"kind":"Static Blender CC0 derivative export; not Unity or real gameplay evidence","coordinateContract":"Runtime wrapper scale1; authored root X reflection cancels glTF Unity handedness; +Z muzzle; metres","weapons":rows,
         "blade":{"source":"Quaternius Ultimate Gun Pack Accessories/Bayonet.blend","length":.5,"gripUnity":[0,0,-.105]},
         "originalMaterials":"Upstream colours/textures retained; optical Glass alpha adapted for FPS visibility. Newly authored feed parts/coils have separately named project materials."}
 (ROOT/"Tools/VisualAuthoring/weapons-v2-export.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
 print("WEAPONS_V2_EXPORT_COMPLETE",len(rows))
if __name__=="__main__":main()