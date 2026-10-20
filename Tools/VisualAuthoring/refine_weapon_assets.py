"""Refine existing licensed derivatives; source packs and runtime markers stay intact.
Blender --background --disable-autoexec --python refine_weapon_assets.py -- --root <repo>
"""
import argparse, sys, json, math, hashlib
from pathlib import Path
import bpy
from mathutils import Vector

parser=argparse.ArgumentParser();parser.add_argument('--root',type=Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]);root=args.root
folder=root/'Game/Assets/_SwapHunter/Resources/VisualV2/Weapons'
review=root/'work/models-october';review.mkdir(parents=True,exist_ok=True)

def material(name,color,metal,rough):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    return m
def B(v):return Vector((v[0],-v[2],v[1]))
def box(name,pos,size,parent,mat):
    bpy.ops.mesh.primitive_cube_add(size=1);o=bpy.context.object;o.name=name;o.parent=parent;o.location=B(pos);o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
    bevel=o.modifiers.new('CNC softened edge','BEVEL');bevel.width=.001;bevel.segments=3;bpy.ops.object.modifier_apply(modifier=bevel.name)
    return o
def bore(name,position,radius,parent,mat):
    # Real uncapped two-wall muzzle bore with an inset throat, no solid muzzle disc.
    verts=[];faces=[];N=32
    for depth,r in ((0,radius),(.009,radius),(.009,radius*.66),(-.009,radius*.66)):
        for j in range(N):
            a=j*2*math.pi/N;verts.append(B(position)+B((math.cos(a)*r,math.sin(a)*r,depth)))
    for ring in range(3):
        for j in range(N):faces.append((ring*N+j,ring*N+(j+1)%N,(ring+1)*N+(j+1)%N,(ring+1)*N+j))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.parent=parent;mesh.materials.append(mat)
    for p in mesh.polygons:p.use_smooth=True
    return obj

rows=[]
for weapon in range(12):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=folder/f'weapon_{weapon:02d}.glb'
    backup=review/(path.stem+'-before.glb')
    if not backup.exists():backup.write_bytes(path.read_bytes())
    bpy.ops.import_scene.gltf(filepath=str(backup))
    model=next(o for o in bpy.context.scene.objects if o.name.startswith('CC0_weapon_'))
    before=sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH')
    # Refine hard edges while retaining each separate magazine/bolt/hinge mesh.
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH':continue
        bpy.context.view_layer.objects.active=obj;obj.select_set(True)
        mod=obj.modifiers.new('October machined bevel','BEVEL');mod.width=.0009 if weapon in (4,11) else .0014;mod.segments=3;mod.limit_method='ANGLE';mod.angle_limit=math.radians(34);mod.use_clamp_overlap=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        normal=obj.modifiers.new('October area-weighted normals','WEIGHTED_NORMAL');normal.keep_sharp=True;normal.weight=35
        try:bpy.ops.object.modifier_apply(modifier=normal.name)
        except RuntimeError:obj.modifiers.remove(normal)
        obj.select_set(False)
    alloy=material('October satin fasteners',(.26,.30,.32),.82,.29)
    dark=material('October inset receiver machining',(.035,.044,.048),.45,.49)
    # Receiver fasteners and a recessed ejection cover keep the twelve silhouettes.
    for side in (-1,1):
        box('Recessed receiver plate',(.053*side,.008,.065),(.003,.047,.095),model,dark)
        for z in (.025,.104):
            bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.0045,depth=.004)
            screw=bpy.context.object;screw.name='Captive slotted receiver screw';screw.parent=model;screw.location=B((.056*side,.008,z));screw.rotation_euler=(0,math.pi/2,0);screw.data.materials.append(alloy)
            box('Screwdriver slot',(.0582*side,.008,z),(.001,.0016,.006),model,dark)
    muzzle=next(o for o in model.children_recursive if o.name=='Muzzle')
    local=model.matrix_world.inverted()@muzzle.matrix_world.translation
    point=Vector((local.x,local.z,-local.y))
    if weapon!=9:bore('Hollow barrel crown',point,.013 if weapon in (1,5,8) else .009,model,alloy)
    marker=bpy.data.objects.new('TriggerContact',None);bpy.context.collection.objects.link(marker);marker.parent=model;marker.location=B((.008,-.059,-.052))
    # Tactile machining follows the existing movable fore-end, not a floating proxy.
    fore=next((o for o in model.children_recursive if o.name=='foregrip'),None)
    if fore:
        for j in range(5):box('Fore-end traction ridge',(0,-.012,-.045+j*.020),(.052,.008,.007),fore,dark)
    if weapon==5:
        sys.path.insert(0,str(Path(__file__).resolve().parent))
        from fix_break_action_details import attach_details
        attach_details(model)
    bpy.context.view_layer.update();bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_animations=False,export_extras=True)
    after=sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH')
    rows.append(dict(id=weapon,source='Existing CC0 derivative + original project detailing',facesBefore=before,facesAfter=after,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    print('REFINED_WEAPON',weapon,before,after,flush=True)
(review/'weapon-refinement-report.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print('WEAPON_REFINEMENT_COMPLETE',flush=True)
