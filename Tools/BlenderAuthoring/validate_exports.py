"""Read actual GLB node/accessor contracts; no Unity or build side effects."""
import json,pathlib,struct,math
ROOT=pathlib.Path(__file__).resolve().parent
PROJECT=ROOT.parent.parent
OUT=PROJECT/'Game/Assets/_SwapHunter/Art/BlenderModels'
manifest=json.loads((OUT/'asset-manifest.json').read_text(encoding='utf-8'))
checks=[];summary=[]
def check(name,value,detail=''):
 checks.append({'name':name,'passed':bool(value),'detail':detail})
for entry in manifest['assets']:
 name=entry['id'];path=OUT/(name+'.glb');b=path.read_bytes()
 magic,version,length=struct.unpack_from('<III',b)
 check(name+'_glb_header',magic==0x46546C67 and version==2 and length==len(b))
 n,t=struct.unpack_from('<II',b,12);data=json.loads(b[20:20+n]);nodes={o.get('name'):o for o in data['nodes']}
 for key,position in [('AxisRight',[1,0,0]),('AxisUp',[0,1,0]),('AxisForward',[0,0,1])]:
  check(name+'_'+key,key in nodes and all(abs(a-c)<1e-5 for a,c in zip(nodes[key].get('translation',[0,0,0]),position)))
 required=['Muzzle','AimRear','AimFront','magazine','charging_handle','muzzle','sight','bolt'] if name.startswith('weapon') else ['hand_L','hand_R','forearm_L','forearm_R'] if name.startswith('operative') else ['leg_L','leg_R','head','torso','arm_L','arm_R','weapon_pivot','EnemyMuzzle','knee_L','knee_R','elbow_L','elbow_R']
 check(name+'_functional_nodes',all(k in nodes for k in required),','.join(k for k in required if k not in nodes))
 triangles=sum(data['accessors'][p['indices']]['count']//3 for mesh in data['meshes'] for p in mesh['primitives'])
 check(name+'_triangle_count',triangles==entry['triangles'],str(triangles))
 check(name+'_normal_attributes',all('NORMAL' in p['attributes'] for mesh in data['meshes'] for p in mesh['primitives']))
 relative=pathlib.Path(entry['blend_source']);blend=PROJECT/relative
 check(name+'_project_relative_source',not relative.is_absolute())
 check(name+'_editable_blend',not relative.is_absolute() and blend.is_file() and blend.stat().st_size>10000)
 check(name+'_finite_bounds',all(math.isfinite(x) for axis in entry['bounds'].values() for x in axis))
 if name.startswith('weapon'):
  check(name+'_forward_muzzle',nodes['Muzzle']['translation'][2]>.4)
  check(name+'_level_sights',abs(nodes['AimFront']['translation'][1]-nodes['AimRear']['translation'][1])<1e-5)
 if name.startswith('robot'):
  index={o.get('name'):i for i,o in enumerate(data['nodes'])};desc=set()
  def children(i):
   for child in data['nodes'][i].get('children',[]):desc.add(child);children(child)
  children(index['weapon_pivot']);check(name+'_muzzle_follows_gun',index['EnemyMuzzle'] in desc)
 summary.append({'id':name,'triangles':triangles,'meshes':len(data['meshes']),'bytes':len(b)})
check('fifteen_assets',len(summary)==15)
report={'passed':all(c['passed'] for c in checks),'scope':'Actual exported GLB contracts and editable Blender files; Unity integration verified separately','checks':checks,'assets':summary}
(ROOT/'asset-contract-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({'passed':report['passed'],'checks':len(checks),'assets':summary},indent=2))
raise SystemExit(0 if report['passed'] else 1)


