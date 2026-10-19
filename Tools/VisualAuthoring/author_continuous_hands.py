"""Continuous, skinned tactical glove authoring. Metres, Blender Z-up, front -Y.
The output glove is one welded surface per hand, with anatomical phalanx chains.
"""
import bpy, math, json
from mathutils import Vector,Matrix,Quaternion

def build(api):
 clear,mat,empty,export=api['clear'],api['mat'],api['empty'],api['export'];preview=api['preview']
 clear();leather=mat('Supple charcoal glove leather',(.032,.039,.044),0,.68);fabric=mat('Woven navy sleeve',(.065,.082,.095),0,.83);back=mat('Flexible back-of-hand reinforcement',(.080,.093,.102),0,.66);cuff=mat('Wrist closure fabric',(.025,.029,.033),0,.79)
 bones={'Body':((0,0,.90),(0,0,1.04),None),'Chest':((0,0,1.04),(0,0,1.43),'Body')};segments={};finger_chains={};meshes=[]
 def loft(name,centers,radii,material,resolution=20):
  vs=[];fs=[]
  for i,p in enumerate(centers):
   p=Vector(p);t=(Vector(centers[min(i+1,len(centers)-1)])-Vector(centers[max(0,i-1)])).normalized();u=Vector((1,0,0));u=(u-t*u.dot(t)).normalized()
   if u.length<.5:u=Vector((0,1,0)).cross(t).normalized()
   v=t.cross(u).normalized();rx,ry=radii[i]
   for j in range(resolution):
    angle=j*2*math.pi/resolution;vs.append(p+u*(math.cos(angle)*rx)+v*(math.sin(angle)*ry))
  for i in range(len(centers)-1):
   for j in range(resolution):a=i*resolution+j;b=i*resolution+(j+1)%resolution;fs.append((a,b,b+resolution,a+resolution))
  fs.extend([tuple(reversed(range(resolution))),tuple((len(centers)-1)*resolution+j for j in range(resolution))])
  mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);mesh.materials.append(material)
  for f in mesh.polygons:f.use_smooth=True
  return o
 for side,sign in [('R',-1),('L',1)]:
  x=sign*.29;hand='hand_'+side;fore='forearm_'+side;upper='UpperArm.'+side
  bones[upper]=((sign*.235,0,1.44),(sign*.28,-.015,1.13),'Chest');bones[fore]=((sign*.28,-.015,1.13),(x,-.05,.86),upper);bones[hand]=((x,-.05,.86),(x,-.05,.765),fore)
  sleeve=loft('Continuous sleeve '+side,[(sign*.243,0,1.39),(sign*.258,-.005,1.30),(sign*.278,-.013,1.16),(sign*.28,-.015,1.13),(sign*.286,-.030,1.02),(x,-.044,.90)],[(.058,.050),(.060,.051),(.053,.046),(.050,.044),(.050,.042),(.038,.034)],fabric,28);sleeve['part']='sleeve';sleeve['side']=side;meshes.append(sleeve)
  cuff_obj=loft('Tailored glove cuff '+side,[(x,-.045,.907),(x,-.049,.885),(x,-.05,.868)],[(.040,.035),(.042,.036),(.034,.027)],cuff,28);cuff_obj['part']='cuff';cuff_obj['side']=side;meshes.append(cuff_obj)
  palm=loft('Continuous glove '+side,[(x,-.050,.880),(x,-.051,.858),(x,-.052,.834),(x,-.052,.809),(x,-.052,.790),(x,-.052,.782)],[(.030,.018),(.032,.020),(.039,.023),(.044,.022),(.043,.019),(.038,.016)],leather,36);parts=[palm]
  # Index-to-little knuckle arch, independent lengths and slim webbing.
  lengths=[.074,.082,.076,.060];lateral=[.034,.011,-.012,-.035];base_z=[.785,.779,.782,.792]
  for fi in range(1,5):
   total=lengths[fi-1];px=x-sign*lateral[fi-1];z=base_z[fi-1];h=Vector((px,-.052,z));points=[h];L=[total*.44,total*.32,total*.24]
   for j,length in enumerate(L):points.append(points[-1]+Vector((sign*(fi-2.5)*.04,-math.sin(math.radians(5+j*7)),-math.cos(math.radians(5+j*7)))).normalized()*length)
   names=['Finger%d%s.%s'%(fi,suffix,side) for suffix in ('','_mid','_tip')];finger_chains[(side,fi)]=names
   for j,n in enumerate(names):bones[n]=(tuple(points[j]),tuple(points[j+1]),hand if j==0 else names[j-1])
   radius=[.0093,.0098,.0093,.0082][fi-1];centers=[points[0]-(points[1]-points[0]).normalized()*.012];radii=[(radius*1.12,radius*.99)]
   for j in range(3):
    for step in (0,.5):centers.append(points[j].lerp(points[j+1],step));r=radius*(1-.11*j);radii.append((r,r*.88))
   centers.extend([points[-1],points[-1]+(points[-1]-points[-2]).normalized()*.004]);radii.extend([(radius*.64,radius*.64),(.0015,.0015)])
   parts.append(loft('Finger loft '+str(fi)+side,centers,radii,leather,24))
  # Thumb originates on the radial side and crosses the palm, not a fifth parallel finger.
  thumb=[Vector((x-sign*.030,-.052,.836)),Vector((x-sign*.054,-.061,.816)),Vector((x-sign*.069,-.071,.795)),Vector((x-sign*.074,-.078,.776))]
  names=['Finger0%s.%s'%(suffix,side) for suffix in ('','_mid','_tip')];finger_chains[(side,0)]=names
  for j,n in enumerate(names):bones[n]=(tuple(thumb[j]),tuple(thumb[j+1]),hand if j==0 else names[j-1])
  parts.append(loft('Opposed thumb loft '+side,thumb+[(thumb[-1]+(thumb[-1]-thumb[-2]).normalized()*.004)],[(.017,.016),(.014,.012),(.012,.011),(.009,.009),(.002,.002)],leather,28))
  # Fuse finger roots, thenar pad and palm into a continuous glove with smooth skinning topology.
  bpy.ops.object.select_all(action='DESELECT')
  for o in parts:o.select_set(True)
  bpy.context.view_layer.objects.active=palm;bpy.ops.object.join();remesh=palm.modifiers.new('Continuous glove surface','REMESH');remesh.mode='VOXEL';remesh.voxel_size=.0015;remesh.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=remesh.name)
  smooth=palm.modifiers.new('Soft leather transitions','SMOOTH');smooth.factor=.55;smooth.iterations=3;bpy.ops.object.modifier_apply(modifier=smooth.name)
  dec=palm.modifiers.new('Glove surface budget','DECIMATE');dec.ratio=min(1,14500/max(1,len(palm.data.polygons)));bpy.ops.object.modifier_apply(modifier=dec.name)
  palm.data.materials.append(back)
  for poly in palm.data.polygons:poly.use_smooth=True;poly.material_index=1 if poly.center.y>-.043 and .79<poly.center.z<.854 else 0
  palm['part']='hand';palm['side']=side;meshes.append(palm)
 rigdata=bpy.data.armatures.new('TacticalSkeleton');rig=bpy.data.objects.new('TacticalSkeleton',rigdata);bpy.context.collection.objects.link(rig);bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
 for n,(h,t,parent) in bones.items():
  b=rigdata.edit_bones.new(n);b.head=h;b.tail=t
  if parent:b.parent=rigdata.edit_bones[parent]
 bpy.ops.object.mode_set(mode='OBJECT')
 def distance(p,a,b):v=b-a;t=max(0,min(1,(p-a).dot(v)/max(.0000001,v.length_squared)));return (p-(a+v*t)).length
 for obj in meshes:
  side=obj['side'];part=obj['part'];allowed=['UpperArm.'+side,'forearm_'+side] if part=='sleeve' else ['forearm_'+side,'hand_'+side] if part=='cuff' else ['hand_'+side]+[n for n in bones if n.startswith('Finger') and n.endswith('.'+side)]
  groups={n:obj.vertex_groups.new(name=n) for n in allowed}
  for v in obj.data.vertices:
   if part!='hand':
    ranked=sorted((distance(v.co,Vector(bones[n][0]),Vector(bones[n][1])),n) for n in allowed)[:2]
    weights=[math.exp(-max(0,d-ranked[0][0])**2/(.035*.035)) for d,n in ranked];total=sum(weights)
    for (d,n),w in zip(ranked,weights):groups[n].add([v.index],w/total,'REPLACE')
   else:
    if v.co.z>.853:
     groups['hand_'+side].add([v.index],1,'REPLACE');continue
    # Never blend one finger's vertices with a neighbouring finger: their closed
    # rotations differ and would shear the palm into spikes. Use one anatomical
    # chain, its adjacent phalanges, and the hand at its metacarpal transition.
    chosen=min(range(5),key=lambda fi:min(distance(v.co,Vector(bones[n][0]),Vector(bones[n][1])) for n in finger_chains[(side,fi)]))
    chain=finger_chains[(side,chosen)];h=Vector(bones[chain[0]][0]);lengths=[(Vector(bones[n][1])-Vector(bones[n][0])).length for n in chain];cumulative=[0,lengths[0],lengths[0]+lengths[1]]
    nearest=min(range(3),key=lambda j:distance(v.co,Vector(bones[chain[j]][0]),Vector(bones[chain[j]][1])))
    start=Vector(bones[chain[nearest]][0]);vec=Vector(bones[chain[nearest]][1])-start;t=max(0,min(1,(v.co-start).dot(vec)/vec.length_squared));path=cumulative[nearest]+t*lengths[nearest]
    if chosen==0:
     radial=abs(v.co.x-(.29 if side=='L' else -.29));blend=max(0,min(1,(radial-.025)/.022))
    else:blend=max(0,min(1,(h.z+.010-v.co.z)/.024))
    blend=blend*blend*(3-2*blend)
    centres=[lengths[0]*.35,cumulative[1]+lengths[1]*.35,cumulative[2]+lengths[2]*.35]
    if path<=centres[0]:weights=[1,0,0]
    elif path<=centres[1]:w=(path-centres[0])/(centres[1]-centres[0]);weights=[1-w,w,0]
    elif path<=centres[2]:w=(path-centres[1])/(centres[2]-centres[1]);weights=[0,1-w,w]
    else:weights=[0,0,1]
    groups['hand_'+side].add([v.index],1-blend,'REPLACE')
    for n,w in zip(chain,weights):
     if w*blend>0:groups[n].add([v.index],w*blend,'REPLACE')
  m=obj.modifiers.new('Anatomical glove skinning','ARMATURE');m.object=rig;obj.parent=rig
 def marker(name,point,bone,rotation=None):
  o=empty(name,point);o.parent=rig;o.parent_type='BONE';o.parent_bone=bone;o.matrix_world=Matrix.Translation(Vector(point))@(rotation.to_matrix().to_4x4() if rotation else Matrix.Identity(4));return o
 for side,sign in [('R',-1),('L',1)]:
  x=sign*.29;point=(x,-.073,.813);marker('PalmContact_'+side,point,'hand_'+side);marker('PalmFrame_'+side,point,'hand_'+side,Quaternion(Vector((0,1,0)),-sign*math.pi/2));grip_rotation=Quaternion(Vector((0,1,0)),-sign*math.pi/2);marker('PalmToolUp_'+side,Vector(point)+grip_rotation@Vector((0,0,.03)),'hand_'+side);marker('PalmToolForward_'+side,Vector(point)+grip_rotation@Vector((0,-.03,0)),'hand_'+side)
  marker('wrist_'+side,bones['hand_'+side][0],'hand_'+side);marker('elbow_'+side,bones['forearm_'+side][0],'forearm_'+side)
  marker('FingerIndexTip_'+side,bones['Finger1_tip.'+side][1],'Finger1_tip.'+side)
 # Direction points survive coordinate-handedness conversion independently of empty rotations.
 def pose_axes(prefix):
  for side in ('R','L'):
   for fi in range(5):
    for n in finger_chains[(side,fi)]:
     pb=rig.pose.bones[n];parent=bones[n][2]
     marker(prefix+'Head_'+n,pb.head,parent);marker(prefix+'Tail_'+n,pb.tail,parent);marker(prefix+'Side_'+n,pb.head+pb.matrix.to_3x3()@Vector((.03,0,0)),parent)
 pose_axes('Open')
 # Produce local closed/trigger poses as real transforms. Runtime interpolates imported rotations.
 def set_closed(side,fi,trigger=False):
  chain=finger_chains[(side,fi)];sign=-1 if side=='R' else 1
  if fi==0:
   directions=[Vector((-sign*.43,-.76,-.48)),Vector((sign*.46,-.75,-.43)),Vector((sign*.89,-.43,-.16))]
  else:
   angles=[62,137,182] if not trigger or fi!=1 else [47,84,111]
   directions=[Vector((0,-math.sin(math.radians(t)),-math.cos(math.radians(t)))) for t in angles]
  for n,wanted in zip(chain,directions):
   pb=rig.pose.bones[n];current=(rig.matrix_world.to_3x3()@(pb.tail-pb.head)).normalized();turn=current.rotation_difference(wanted.normalized());matrix=pb.matrix.copy();rotation=turn.to_matrix().to_4x4();matrix=Matrix.Translation(matrix.translation)@rotation@Matrix.Translation(-matrix.translation)@matrix;pb.matrix=matrix;bpy.context.view_layer.update()
 for mode in ('Closed','Trigger'):
  for pb in rig.pose.bones:pb.rotation_mode='QUATERNION';pb.rotation_quaternion=Quaternion()
  for side in ('R','L'):
   for fi in range(5):set_closed(side,fi,mode=='Trigger')
  bpy.context.view_layer.update()
  for side in ('R','L'):
   for fi in range(5):
    for n in finger_chains[(side,fi)]:
     pb=rig.pose.bones[n];parent=bones[n][2];m=pb.matrix.copy();o=marker(mode+'Pose_'+n,m.translation,parent,m.to_quaternion())
  pose_axes(mode)
  if mode=='Closed':bpy.ops.wm.save_as_mainfile(filepath=str(preview/'fps_hands_closed_review.blend'))
 for pb in rig.pose.bones:pb.rotation_quaternion=Quaternion()
 bpy.context.view_layer.update();rig.location=(.23,.60,-1.39)
 export('fps_arms','Characters',anim=False);bpy.ops.wm.save_as_mainfile(filepath=str(preview/'fps_arms.blend'))
 api['report']['handDesign']='Continuous voxel-unioned glove surface, anatomical 4-finger + opposed-thumb chains; authored Closed/Trigger pose markers; no separated sphere joints.'
 print('CONTINUOUS_HANDS_COMPLETE',flush=True)
