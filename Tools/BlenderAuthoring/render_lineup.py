import bpy,pathlib,math
from mathutils import Vector,Matrix
HERE=pathlib.Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
names=['BURST RIFLE','HEAVY RIFLE','SMG','DOUBLE BARREL','DMR','BOLT SNIPER','LMG','GRENADE LAUNCHER','CHARGE RIFLE','PHASE MARKER']
for i in range(10):
 asset='weapon_'+str(i+2).zfill(2)
 with bpy.data.libraries.load(str(HERE/'blend'/(asset+'.blend')),link=False) as (src,dst):dst.objects=src.objects
 for obj in dst.objects:
  if obj:bpy.context.collection.objects.link(obj)
 root=next(o for o in dst.objects if o and o.name==asset);root.location=Vector((0,(i%2)*2.30,-(i//2)*.79))
 text=bpy.data.curves.new(asset+' label','FONT');text.body=str(i+2).zfill(2)+' / '+names[i];text.size=.085;text.extrude=.0005
 obj=bpy.data.objects.new(asset+' label',text);bpy.context.collection.objects.link(obj)
 obj.matrix_world=Matrix(((0,0,1,.22),(1,0,0,(i%2)*2.30-.96),(0,1,0,-(i//2)*.79+.34),(0,0,0,1)))
 mat=bpy.data.materials.new('Label '+asset);mat.diffuse_color=(.65,.85,.82,1);text.materials.append(mat)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True;scene.render.resolution_x=1700;scene.render.resolution_y=1500;scene.render.resolution_percentage=100
target=Vector((0,.83,-1.52));bpy.ops.object.camera_add(location=target+Vector((8,0,0)));camera=bpy.context.object;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=4.80;scene.camera=camera
for name,pos,power,size in [('Key',(4,1,3),700,5),('Fill',(2,-3,-1),300,4),('Edge',(-2,1,1),350,4)]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
scene.world.color=(.055,.065,.075);scene.render.image_settings.file_format='PNG';scene.render.filepath=str(HERE/'previews/weapon-lineup.png')
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'blend/weapon-lineup.blend'),check_existing=False);bpy.ops.render.render(write_still=True)
print('SWAPHUNTER_LINEUP_RENDERED',flush=True)

