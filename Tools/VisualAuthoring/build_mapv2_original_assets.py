"""Original MapV2 tactical bodies and six real knife assets. No downloaded/proprietary geometry.
Blender --background --factory-startup --python this.py -- --out <Resources/VisualMapV2>
"""
import bpy, math, json, argparse, sys
from pathlib import Path
from mathutils import Vector, Matrix
P=argparse.ArgumentParser();P.add_argument('--out',required=True);P.add_argument('--preview',required=True);P.add_argument('--only',default='')
a=P.parse_args(sys.argv[sys.argv.index('--')+1:]);out=Path(a.out);out.mkdir(parents=True,exist_ok=True);preview=Path(a.preview);preview.mkdir(parents=True,exist_ok=True)
report={'origin':'Original procedural authored meshes and animation by SwapHunter project; no third-party input','models':[]}
def clear(): bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(n,c,metal=0,rough=.5):
 m=bpy.data.materials.new(n);m.diffuse_color=(*c,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;return m
def finish(o,n,m,bone=None):
 o.name=n;o.data.materials.append(m)
 if bone: o['bone']=bone
 for p in o.data.polygons:p.use_smooth=True
 return o
def sphere(n,p,s,m,bone=None):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,location=p);o=bpy.context.object;o.scale=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,n,m,bone)
def box(n,p,s,m,bev=.012,bone=None):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.scale=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bev:
  md=o.modifiers.new('Machined edge bevel','BEVEL');md.width=bev;md.segments=3;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=md.name)
 return finish(o,n,m,bone)
def rod(n,p,q,r,m,bone=None,vertices=24):
 d=Vector(q)-Vector(p)
 if n.startswith(('Upper arm ','Sleeve forearm ','Thigh ','Trouser shin ')):
  mid=(Vector(p)+Vector(q))/2;o=sphere(n,mid,(r*1.02,r*.90,d.length*.5+.047),m,bone);o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return o
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(Vector(p)+Vector(q))/2);o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,n,m,bone)
def ring(n,p,major,minor,m,rotation=(0,0,0),bone=None):
 bpy.ops.mesh.primitive_torus_add(major_segments=64,minor_segments=16,major_radius=major,minor_radius=minor,location=p,rotation=rotation);return finish(bpy.context.object,n,m,bone)
def empty(n,p,parent=None):
 o=bpy.data.objects.new(n,None);bpy.context.collection.objects.link(o);o.location=p
 if parent:o.parent=parent
 return o
def parent_keep(o,p):w=o.matrix_world.copy();o.parent=p;o.matrix_world=w

def blade(n,outline,thickness,m,edge,bevel=.001):
 # Four profile rings form a genuinely thick spine, wedge bevels and a fine cutting edge.
 N=len(outline);cx=sum(x for x,z in outline)/N;cz=sum(z for x,z in outline)/N;verts=[]
 for depth,scale in [(-thickness*.5,.82),(-.00035,1),(.00035,1),(thickness*.5,.82)]:
  verts += [(cx+(x-cx)*scale,depth,cz+(z-cz)*scale) for x,z in outline]
 faces=[]
 for k in range(3):
  for j in range(N): faces.append((k*N+j,k*N+(j+1)%N,(k+1)*N+(j+1)%N,(k+1)*N+j))
 faces.extend([tuple(reversed(range(N))),tuple(range(3*N,4*N))]);mesh=bpy.data.meshes.new(n);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(n,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(m);o.data.materials.append(edge)
 for p in o.data.polygons:p.material_index=1 if p.index<N*3 else 0
 b=o.modifiers.new('Polished cutting edge','BEVEL');b.width=bevel;b.segments=3;bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.modifier_apply(modifier=b.name);o.select_set(False);return o

def export(name,sub,root=None,anim=False):
 dst=out/sub/(name+'.glb');dst.parent.mkdir(parents=True,exist_ok=True)
 bpy.ops.object.select_all(action='SELECT')
 for o in bpy.context.scene.objects:
  if o.type in ('LIGHT','CAMERA'):o.select_set(False)
 bpy.ops.export_scene.gltf(filepath=str(dst),export_format='GLB',use_selection=True,export_yup=True,export_apply=False,export_animations=anim,export_animation_mode='ACTIONS',export_force_sampling=True,export_anim_single_armature=True,export_materials='EXPORT',export_skins=True)
 triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH');record={'id':name,'path':str(dst),'triangles':triangles,'bytes':dst.stat().st_size,'parts':[o.name for o in bpy.context.scene.objects if o.type=='MESH']};report['models'].append(record);return dst

def knife(i):
 clear();steel=mat('Brushed blade steel',(.36,.42,.46),.94,.23);edge=mat('Honed bright edge',(.68,.74,.79),1,.17);rubber=mat('Milled tactical grip',(.035,.045,.049),.10,.62);brass=mat('Grip fasteners',(.41,.31,.16),.9,.31);rope=mat('Braided olive lanyard',(.20,.25,.11),0,.85)
 if i==1:steel=mat('Opaque layered emerald steel',(.015,.32,.22),.92,.16)
 root=empty('KnifeRoot',(0,0,0));objects=[];grip=empty('Grip',(.014 if i==3 else 0,0,-.045 if i==2 else 0));tipz=.31
 if i==2:
  outline=[(-.018,.062),(-.039,.13),(-.048,.21),(-.026,.27),(.026,.29),(.011,.253),(-.009,.231),(-.015,.19),(.014,.11),(.019,.062)];tipz=.29
 elif i==1: outline=[(-.033,.075),(-.033,.27),(-.015,.30),(.005,.35),(.035,.25),(.034,.075)];tipz=.35
 elif i==5:
  outline=[(-.023,.074)];
  for j in range(9):outline.extend([(-.023,.085+j*.018),(-.034,.092+j*.018),(-.023,.099+j*.018)])
  outline.extend([(-.02,.27),(0,.335),(.03,.267),(.031,.074)]);tipz=.335
 else: outline=[(-.02,.071),(-.02,.25),(0,.315),(.022,.246),(.022,.071)]
 b=blade('Blade',outline,.006 if i!=1 else .008,steel,edge);objects.append(b)
 if i==3:
  # Independent handles pivot about the actual tang hinges. Open at rest; Fold drives +180/-180.
  for side in (-1,1):
   hinge=empty('HandleHinge'+('A' if side<0 else 'B'),(side*.014,0,.075));h=box('Butterfly split handle '+str(side),(side*.014,0,-.005),(.022,.024,.158),rubber,.006);parent_keep(h,hinge);objects.append(hinge)
   for j in range(6):
    riv=rod('Handle through insert',(side*.014,-.013,-.065+j*.023),(side*.014,.013,-.065+j*.023),.0045,brass,vertices=20);parent_keep(riv,hinge)
   pin=rod('Hinge pivot',(side*.014,-.019,.075),(side*.014,.019,.075),.006,edge,vertices=32);objects.append(pin)
  latch=box('Latch',(.026,.0,-.088),(.025,.010,.012),steel,.002);objects.append(latch)
 else:
  h=sphere('Ergonomic handle',(0,0,0),(.024,.020,.078),rubber);objects.append(h)
  for j in range(9):
   tor=ring('Raised nonslip grip rib',(0,0,-.060+j*.014),.020,.0016,rubber);tor.scale.x=1.12;tor.scale.y=.88;objects.append(tor)
  guard=box('Full tang hand guard',(0,0,.073),(.082 if i==1 else .062,.029,.012),steel,.003);objects.append(guard)
  pom=box('Solid tang pommel',(0,0,-.079),(.035,.030,.016),steel,.004);objects.append(pom)
  for z in (-.042,.035):objects.append(rod('Grip scale rivet',(-.025,0,z),(.025,0,z),.003,brass,vertices=24))
 if i==2:
  objects.append(ring('Finger ring',(.002,0,-.110),.025,.005,steel,(math.pi/2,0,0)));objects.append(empty('Ring',(.002,0,-.110)))
 if i==4:
  objects.append(ring('Lanyard grommet',(0,0,-.083),.008,.002,edge,(math.pi/2,0,0)))
  for k in range(2):
   for j in range(12):
    z=-.09-j*.007;x=.012*math.sin(j*.25)+(.003 if k else -.003);p=(x,k*.004,z);q=(.012*math.sin((j+1)*.25)+(.003 if k else -.003),k*.004,z-.007);objects.append(rod('Lanyard segment %02d_%d'%(j,k),p,q,.0028,rope,vertices=12))
  objects.append(sphere('Lanyard knot',(.010,.002,-.12),(.008,.006,.008),rope))
  objects.append(empty('LanyardEnd',(.012,0,-.177)))
 for o in list(bpy.context.scene.objects):
  if o!=root and not o.parent:parent_keep(o,root)
 empty('Tip',(0,0,tipz),root);empty('BladeDanger',(0,0,.19),root);empty('Palm',(0,-.015,0),root)
 # Blender Z blade becomes Unity Y; blade lies in X/Y plane for grasp-friendly upright knives.
 export('knife_%02d'%i,'Knives');bpy.ops.wm.save_as_mainfile(filepath=str(preview/('knife_%02d.blend'%i)))
 return

# Character authored in Blender coordinates X mirror to Unity, front -Y, Z up.
def human(name,variant=0,fp=False,body_only=False):
 clear();cloth=mat('Charcoal technical fabric',(.075,.093,.108),0,.87);plate=mat('Slate ceramic armor',(.16,.20,.23),.32,.42);trim=mat('Muted identification',(.07,.27,.33),.45,.34);skin=mat('Natural skin',(.48,.29,.19),0,.63);black=mat('Black rubber',(.023,.027,.030),.04,.75);visor=mat('Tinted visor',(.065,.13,.16),.65,.15);steel=mat('Equipment alloy',(.25,.28,.30),.72,.32)
 bones={'Body':((0,0,.90),(0,0,1.04),None),'Chest':((0,0,1.04),(0,0,1.43),'Body'),'Neck':((0,0,1.43),(0,0,1.55),'Chest'),'Head':((0,0,1.55),(0,0,1.78),'Neck')}
 for side,sign in [('R',-1),('L',1)]:
  bones['UpperArm.'+side]=((sign*.235,0,1.44),(sign*.28,-.015,1.13),'Chest');bones['LowerArm.'+side]=((sign*.28,-.015,1.13),(sign*.29,-.05,.86),'UpperArm.'+side);bones['Fist.'+side]=((sign*.29,-.05,.86),(sign*.29,-.065,.76),'LowerArm.'+side)
  bones['UpperLeg.'+side]=((sign*.10,0,.94),(sign*.105,-.015,.53),'Body');bones['LowerLeg.'+side]=((sign*.105,-.015,.53),(sign*.11,.005,.12),'UpperLeg.'+side);bones['Foot.'+side]=((sign*.11,.005,.12),(sign*.11,-.16,.055),'LowerLeg.'+side)
  for f in range(5):
   x=sign*(.265+f*.013);z=.815 if f==0 else .790
   h=(x,-.062,z);t1=(x,-.071,z-.022);t2=(x,-.097,z-.026);t3=(x,-.111,z-.005)
   bones['Finger%d.%s'%(f,side)]=(h,t1,'Fist.'+side)
   bones['Finger%d_mid.%s'%(f,side)]=(t1,t2,'Finger%d.%s'%(f,side))
   bones['Finger%d_tip.%s'%(f,side)]=(t2,t3,'Finger%d_mid.%s'%(f,side))
 if not fp:
  sphere('Suit torso',(0,0,1.23),(.205,.118,.245),cloth,'Chest');sphere('Pelvic suit',(0,0,.95),(.158,.113,.135),cloth,'Body')
  box('Chest armored plate',(0,-.112,1.29),(.315,.055,.285),plate,.035,'Chest');box('Rear plate',(0,.11,1.28),(.30,.060,.28),plate,.026,'Chest')
  rod('Collar',(0,0,1.435),(0,0,1.565),.061,cloth,'Neck');sphere('Human head',(0,-.005,1.655),(.084,.088,.116),skin,'Head')
  sphere('Helmet shell',(0,.008,1.683),(.093,.093,.113),plate,'Head');box('Upper visor frame',(0,-.085,1.685),(.156,.022,.025),black,.008,'Head');sphere('Visor',(0,-.084,1.656),(.077,.014,.037),visor,'Head')
  box('Chin strap',(0,-.067,1.575),(.096,.020,.022),black,.006,'Head')
  box('Belt',(0,-.003,1.002),(.325,.245,.040),black,.012,'Body');box('Buckle',(0,-.136,1.005),(.045,.018,.035),steel,.005,'Body')
  for x in (-.105,0,.105):box('Magazine pouch',(x,-.15,1.145),(.077,.050,.100),cloth,.012,'Chest')
  box('Identity rescue stripe',(0,-.145,1.395),(.13,.011,.020),trim,.002,'Chest')
 for side,sign in [('R',-1),('L',1)]:
  arm='UpperArm.'+side;fore='LowerArm.'+side;hand='Fist.'+side
  sphere('Shoulder fabric '+side,(sign*.218,0,1.425),(.085,.095,.095),cloth,arm)
  rod('Upper arm '+side,(sign*.235,0,1.405),(sign*.28,-.015,1.15),.061,cloth,arm)
  sphere('Shoulder plate '+side,(sign*.246,0,1.445),(.077,.092,.054),plate,arm)
  sphere('Elbow '+side,(sign*.28,-.015,1.13),(.052,.055,.057),black,fore)
  rod('Sleeve forearm '+side,(sign*.282,-.02,1.10),(sign*.29,-.05,.89),.048,cloth,fore)
  box('Forearm armor '+side,(sign*.29,.012,1.00),(.077,.034,.146),plate,.018,fore)
  rod('Wrist cuff '+side,(sign*.29,-.05,.89),(sign*.29,-.055,.825),.034,black,hand)
  sphere('Glove palm '+side,(sign*.29,-.055,.815),(.044,.025,.052),black,hand)
  box('Knuckle guard '+side,(sign*.29,-.025,.804),(.070,.014,.035),plate,.008,hand)
  for f in range(5):
   for section in ('','_mid','_tip'):
    bn='Finger%d%s.%s'%(f,section,side);h,t,_=bones[bn]
    rod('Glove phalanx '+bn,h,t,.0085 if f else .010,black,bn,16)
    sphere('Glove knuckle '+bn,h,(.009,.009,.009),black,bn)
   sphere('Finger padded tip %d %s'%(f,side),t,(.008,.009,.010),black,bn)
  if not fp:
   thigh='UpperLeg.'+side;shin='LowerLeg.'+side;foot='Foot.'+side
   rod('Thigh '+side,(sign*.10,0,.91),(sign*.105,-.015,.55),.088,cloth,thigh);sphere('Knee '+side,(sign*.105,-.015,.53),(.073,.068,.070),black,shin)
   box('Knee pad '+side,(sign*.105,-.072,.54),(.102,.036,.106),plate,.023,shin);rod('Trouser shin '+side,(sign*.108,0,.49),(sign*.11,.005,.16),.062,cloth,shin)
   box('Boot '+side,(sign*.11,-.06,.083),(.118,.245,.155),black,.029,foot);box('Boot sole '+side,(sign*.11,-.066,.024),(.125,.252,.035),black,.009,foot)
   if variant==0:box('Leg assistance rail '+side,(sign*.18,.012,.36),(.028,.033,.30),steel,.006,shin)
 if not fp:
  if variant==1:
   for sign in (-1,1):box('Heavy shoulder wing',(sign*.27,0,1.45),(.15,.18,.09),plate,.022,'UpperArm.'+('R' if sign<0 else 'L'))
   box('Heavy support pack',(0,.166,1.24),(.28,.115,.31),plate,.023,'Chest')
  if variant==2:
   box('Recon optic',(0,-.102,1.70),(.122,.038,.033),steel,.006,'Head');rod('Recon antenna',(.14,.12,1.39),(.14,.12,1.68),.006,steel,'Chest')
  if variant==3:
   for sign in (-1,1):
    for z in (1.10,1.24,1.38):rod('Ordnance canister',(sign*.20,.14,z),(sign*.20,.20,z),.038,steel,'Chest')
 if fp:
  for o in list(bpy.context.scene.objects):
   if o.type=='MESH' and o.name.startswith(('Shoulder fabric ','Shoulder plate ')):bpy.data.objects.remove(o,do_unlink=True)
 if body_only:
  for o in list(bpy.context.scene.objects):
   if o.type=='MESH' and (o.get('bone') in ('Head','Neck') or o.get('bone','').startswith(('UpperArm.','LowerArm.','Fist.','Finger'))):bpy.data.objects.remove(o,do_unlink=True)
 objs=[o for o in bpy.context.scene.objects if o.type=='MESH'];rigdata=bpy.data.armatures.new('TacticalSkeleton');rig=bpy.data.objects.new('TacticalSkeleton',rigdata);bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 for n,(h,t,parent) in bones.items():
  b=rigdata.edit_bones.new(n);b.head=h;b.tail=t
  if parent:b.parent=rigdata.edit_bones[parent]
 bpy.ops.object.mode_set(mode='OBJECT')
 for o in objs:
  group=o.vertex_groups.new(name=o['bone']);group.add(list(range(len(o.data.vertices))),1,'REPLACE');m=o.modifiers.new('Deform tactical skeleton','ARMATURE');m.object=rig;o.parent=rig
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();objs=[bpy.context.object];objs[0].name='Tactical human skinned mesh'
 # Exact grip and hit markers parented to anatomy, not inferred from renderer bounds.
 for side in ('L','R'):
  for mark,pt in [('PalmContact_',Vector(bones['Fist.'+side][0]).lerp(Vector(bones['Fist.'+side][1]),.52)),('wrist_',Vector(bones['Fist.'+side][0])),('elbow_',Vector(bones['LowerArm.'+side][0]))]:
   o=empty(mark+side,pt);o.parent=rig;o.parent_type='BONE';o.parent_bone=('LowerArm.' if mark=='elbow_' else 'Fist.')+side;o.matrix_world=Matrix.Translation(pt)
 for n,bn,pt in [('HeadHit','Head',(0,-.007,1.65)),('ChestHit','Chest',(0,0,1.25))]:
  o=empty(n,pt);o.parent=rig;o.parent_type='BONE';o.parent_bone=bn;o.matrix_world=Matrix.Translation(pt)
 if fp:
  for side in ('L','R'):
   for old,new in [('LowerArm.','forearm_'),('Fist.','hand_')]:
    rig.data.bones[old+side].name=new+side
    for o in objs:
     if o.vertex_groups.get(old+side):o.vertex_groups[old+side].name=new+side
  # PlayerWeaponPresentation positions arms at (.23,-.28,.53). Result shoulders align body.
  rig.location=(.23,.60,-1.39)
 else:
  rig.animation_data_create()
  actions=['Idle','Walk','Hit','Shoot','Pickup','Punch','Kick','Jump','SwordSlash','Death']
  for label in actions:
   action=bpy.data.actions.new(label);action.use_fake_user=True;rig.animation_data.action=action
   for frame in (1,7,13,19,25):
    p=(frame-1)/24;sin=math.sin(p*2*math.pi)
    for pb in rig.pose.bones:pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
    if label=='Idle':rig.pose.bones['Chest'].rotation_euler.x=.009*sin
    if label=='Walk':
     for side,sign in [('L',1),('R',-1)]:rig.pose.bones['UpperLeg.'+side].rotation_euler.x=sin*.35*sign;rig.pose.bones['LowerLeg.'+side].rotation_euler.x=max(0,-sin*sign)*.48;rig.pose.bones['UpperArm.'+side].rotation_euler.x=-sin*.25*sign
    if label=='Shoot':rig.pose.bones['Chest'].rotation_euler.x=-math.sin(p*math.pi)*.045
    if label=='Hit':rig.pose.bones['Chest'].rotation_euler.x=-math.sin(p*math.pi)*.12
    if label=='Pickup':rig.pose.bones['Body'].location.z=-math.sin(p*math.pi)*.34;rig.pose.bones['Chest'].rotation_euler.x=.36*math.sin(p*math.pi)
    if label in ('Punch','SwordSlash'):rig.pose.bones['UpperArm.R'].rotation_euler.x=-math.sin(p*math.pi)*1.3;rig.pose.bones['Chest'].rotation_euler.z=math.sin(p*math.pi)*.28
    if label=='Kick':rig.pose.bones['UpperLeg.R'].rotation_euler.x=-math.sin(p*math.pi)*.95
    if label=='Jump':rig.pose.bones['Body'].location.z=math.sin(p*math.pi)*.22
    if label=='Death':rig.pose.bones['Body'].rotation_euler.x=-p*1.48;rig.pose.bones['Body'].location.z=-p*.72
    for pb in rig.pose.bones:pb.keyframe_insert('rotation_euler',frame=frame);pb.keyframe_insert('location',frame=frame)
  rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
 export(name,'Characters' if fp or body_only else 'Actors',anim=not fp and not body_only)
 bpy.ops.wm.save_as_mainfile(filepath=str(preview/(name+'.blend')))
for i in range(6):
 if not a.only or a.only=='knife_%02d'%i:knife(i)
for i in range(4):
 if not a.only or a.only=='class_%02d'%i:human('class_%02d'%i,i)
if not a.only or a.only=='fps_arms':human('fps_arms',0,True)
if a.only and (preview/'asset-report.json').exists():
 old=json.loads((preview/'asset-report.json').read_text(encoding='utf-8'));report['models']=[r for r in old['models'] if r['id']!=a.only]+report['models']
(preview/'asset-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('MAPV2_ORIGINAL_ASSETS_COMPLETE',flush=True)
