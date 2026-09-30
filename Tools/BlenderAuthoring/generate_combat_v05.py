"""Original phase knife. Reuses the established Blender authoring helpers only."""
import pathlib, json
HERE=pathlib.Path(__file__).resolve().parent
source=(HERE/'generate_v04.py').read_text(encoding='utf-8-sig')
exec(compile(source[:source.index("manifest={'generator'")],str(HERE/'generate_v04.py'),'exec'),globals())
setup();root=group('Phase tactical knife')
# Faceted spear point: X width, Y thickness, Z blade length; no borrowed geometry.
verts=[(-.048,0,0),(.048,0,0),(0,0,.32),(0,.022,.07),(0,-.022,.07)]
mesh=bpy.data.meshes.new('Forged spear blade');mesh.from_pydata([tuple(v(p)) for p in verts],[],[(0,3,2),(3,1,2),(0,2,4),(4,2,1),(0,4,3),(3,4,1)]);mesh.update()
blade=bpy.data.objects.new('Forged spear blade',mesh);bpy.context.collection.objects.link(blade);blade.parent=root;blade.data.materials.append(M['steel'])
box(root,'Guard',(.12,.038,.035),(0,0,-.015),'edge',.006)
box(root,'Rubber grip',(.058,.050,.155),(0,0,-.105),'rubber',.008)
for i in range(5):box(root,'Grip ridge',(.061,.054,.009),(0,0,-.05-i*.026),'body',.003)
box(root,'Silence capacitor',(.023,.014,.071),(0,.031,-.09),'cyan',.003)
ring(root,'Recovery loop',.031,.007,(0,0,-.198),'gold','y')
marker(root,'AxisRight',(1,0,0));marker(root,'AxisUp',(0,1,0));marker(root,'AxisForward',(0,0,1));marker(root,'BladeTip',(0,0,.32))
root['author']='SwapHunter original Blender authoring';root['asset_id']='combat-knife-v05';root['license']='MIT';root['units']='meters'
bpy.context.view_layer.update();bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
for item in root.children_recursive:item.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'combat-knife-v05.blend'),check_existing=False)
bpy.ops.export_scene.gltf(filepath=str(OUT/'combat-knife-v05.glb'),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_extras=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
report={'asset':'combat-knife-v05','version':'0.5.0','generator':bpy.app.version_string,'source':'blend/combat-knife-v05.blend','license':'MIT','inputs':'original authored mesh; no external assets','vertices':sum(len(o.data.vertices) for o in root.children_recursive if o.type=='MESH')}
(HERE/'combat-v05-manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
preview(root,'combat-knife-v05')
print('SWAPHUNTER_COMBAT_ASSET_DONE',flush=True)
