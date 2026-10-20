"""Bind front sight and barrel crown to the real double-barrel hinge.
Only visible geometry is reparented; all gameplay grip/sight/muzzle markers stay unchanged.
"""
import bpy
from mathutils import Vector
from pathlib import Path

def parent_keep(obj,parent):
    world=obj.matrix_world.copy();obj.parent=parent;obj.matrix_world=world

def mesh_part(source,polygons,name):
    used=sorted({i for polygon in polygons for i in polygon.vertices});mapping={old:new for new,old in enumerate(used)}
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata([source.data.vertices[i].co.copy() for i in used],[],[[mapping[i] for i in polygon.vertices] for polygon in polygons]);mesh.update()
    for material in source.data.materials:mesh.materials.append(material)
    for old,new in zip(polygons,mesh.polygons):new.material_index=old.material_index;new.use_smooth=old.use_smooth
    return mesh

def attach_details(model):
    bpy.context.view_layer.update()
    hinge=next(obj for obj in model.children_recursive if obj.name=='break_action')
    muzzle=next(obj for obj in model.children_recursive if obj.name=='Muzzle')
    for obj in list(model.children):
        if obj.name.startswith('Hollow barrel crown'):parent_keep(obj,hinge)
        if obj.type!='MESH' or not obj.name.startswith('CC0_weapon_05_geometry'):continue
        # This original joined mesh contains both rear and front sight posts.
        # Split only the polygons beside the actual muzzle; retain the rear post.
        selected=[];remaining=[]
        for polygon in obj.data.polygons:
            center=sum((obj.matrix_world@obj.data.vertices[i].co for i in polygon.vertices),Vector())/len(polygon.vertices)
            (selected if (center-muzzle.matrix_world.translation).length<.15 else remaining).append(polygon)
        if not selected:continue
        front=bpy.data.objects.new('Front sight / hinged barrel',mesh_part(obj,selected,'Hinged front sight mesh'));bpy.context.collection.objects.link(front);front.matrix_world=obj.matrix_world.copy();parent_keep(front,hinge)
        obj.data=mesh_part(obj,remaining,'Fixed rear sight mesh')
    bpy.context.view_layer.update()

if __name__=='__main__':
    path=Path(__file__).resolve().parents[2]/'Game/Assets/_SwapHunter/Resources/VisualV2/Weapons/weapon_05.glb'
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(path))
    model=next(o for o in bpy.context.scene.objects if o.name=='CC0_weapon_05');attach_details(model)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_animations=False,export_extras=True)
    print('BREAK_ACTION_FRONT_DETAILS_ATTACHED',flush=True)
