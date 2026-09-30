"""Original SwapHunter Blender v0.4 authoring. No external game assets.
Author metres/axes X right,Y up,Z forward; Blender mapping (x,-z,y)."""
import bpy,math,json,pathlib,sys,argparse
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent
OUT=HERE.parent.parent/'Game/Assets/_SwapHunter/Art/BlenderModels'
SOURCE=HERE/'blend';PREVIEW=HERE/'previews'
for d in (OUT,SOURCE,PREVIEW):d.mkdir(parents=True,exist_ok=True)
def v(p):return Vector((p[0],-p[2],p[1]))
def material(name,c,metal=.1,rough=.5,emit=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True;b=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) or m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
 b.inputs['Base Color'].default_value=(*c,1);b.inputs['Metallic'].default_value=metal;b.inputs['Roughness'].default_value=rough
 if emit:b.inputs['Emission Color'].default_value=(*c,1);b.inputs['Emission Strength'].default_value=emit
 return m
def setup():
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 for m in list(bpy.data.materials):bpy.data.materials.remove(m)
 global M
 M={'body':material('Cerakote midnight',(.075,.115,.15),.35,.38),'steel':material('Titanium',(.36,.43,.47),.8,.27),'edge':material('Aluminium',(.64,.70,.70),.72,.25),'rubber':material('Elastomer',(.018,.027,.035),0,.8),'ivory':material('Ceramic',(.64,.68,.62),.12,.37),'ochre':material('Ochre armour',(.50,.22,.065),.22,.43),'teal':material('Teal armour',(.09,.31,.34),.28,.42),'cyan':material('Phase glass',(.035,.78,.65),.1,.2,1.6),'red':material('Hostile optics',(.9,.105,.035),.08,.3,1.25),'gold':material('Copper',(.6,.36,.10),.8,.27),'glass':material('Optical glass',(.024,.052,.058),.6,.16)}
 bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
def group(name,parent=None,p=(0,0,0)):
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.parent=parent;o.location=v(p);return o
def assign(o,name,mat,parent,p):
 o.name=name;o.data.materials.append(M[mat]);o.parent=parent;o.location=v(p);return o
def finish(o,bevel=0):
 if bevel:
  mod=o.modifiers.new('Manufactured bevel','BEVEL');mod.width=bevel;mod.segments=2
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 for face in o.data.polygons:face.use_smooth=True
 mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=40
 bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name);return o
def box(parent,name,size,p,mat='body',bevel=.006):
 bpy.ops.mesh.primitive_cube_add(size=1);o=bpy.context.object;assign(o,name,mat,parent,p);o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,min(bevel,min(size)*.22))
def cyl(parent,name,r,length,p,mat='steel',axis='z',vertices=20):
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=length);o=bpy.context.object;assign(o,name,mat,parent,p)
 o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(v({'x':(1,0,0),'y':(0,1,0),'z':(0,0,1)}[axis]));return finish(o,min(.0025,r*.1))
def sphere(parent,name,r,p,mat='rubber',scale=(1,1,1)):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=r);o=bpy.context.object;assign(o,name,mat,parent,p);o.scale=(scale[0],scale[2],scale[1])
 for f in o.data.polygons:f.use_smooth=True
 return o
def link(parent,name,a,b,r,mat='rubber'):
 a,b=Vector(a),Vector(b);o=cyl(parent,name,r,(b-a).length,(a+b)*.5,mat,'y',16);o.rotation_quaternion=Vector((0,0,1)).rotation_difference(v(b-a).normalized());return o
def profile(parent,name,outline,width,mat='body',bevel=.008,x=0):
 n=len(outline);verts=[tuple(v((s*width/2,y,z))) for s in (-1,1) for z,y in outline]
 faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);assign(o,name,mat,parent,(x,0,0));return finish(o,bevel)
def ring(parent,name,r,t,p,mat='steel',axis='z'):
 bpy.ops.mesh.primitive_torus_add(major_radius=r,minor_radius=t,major_segments=24,minor_segments=8);o=bpy.context.object;assign(o,name,mat,parent,p)
 o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(v({'x':(1,0,0),'y':(0,1,0),'z':(0,0,1)}[axis]))
 for f in o.data.polygons:f.use_smooth=True
 return o
def marker(parent,name,p):return group(name,parent,p)
def authored_pivot(parent,p):
 # Parts are first authored in the original root frame, then rebased together.
 # This changes the rotation origin without changing any rest-world geometry.
 offset=v(p);parent.location=offset
 for child in parent.children:child.location-=offset
def screw(parent,p,axis='x'):return cyl(parent,'Torx fastener',.007,.007,p,'edge',axis,8)
def grip(root):
 g=group('pistol_grip',root);box(g,'Grip mounting collar',(.075,.062,.07),(0,-.055,-.09),'body',.004);profile(g,'Ergonomic grip',[(-.145,-.07),(-.058,-.07),(-.016,-.255),(-.095,-.285),(-.16,-.10)],.069,'rubber',.011)
 for i in range(5):box(g,'Grip serration',(.074,.01,.045),(0,-.12-i*.027,-.085+i*.004),'body',.002)
 profile(root,'Trigger guard',[(-.066,-.07),(.072,-.07),(.068,-.157),(-.032,-.177),(-.046,-.148),(.047,-.130),(.051,-.09),(-.055,-.09)],.03,'steel',.003)
 profile(root,'Trigger',[(0,-.085),(.016,-.092),(.007,-.127),(-.008,-.137),(-.014,-.13),(-.003,-.113)],.012,'edge',.002)
def stock(root,style='full',cheek_drop=0):
 if style!='wire':cyl(root,'Stock receiver interface',.033,.11,(0,.024,-.165),'steel')
 if style=='wire':
  for x in (-.046,.046):link(root,'Folding stock strut',(x,.045,-.16),(x,0,-.405),.012,'steel')
  box(root,'Stock heel',(.098,.145,.035),(0,-.008,-.418),'rubber',.011)
 elif style=='skeleton':
  profile(root,'Skeletal stock',[(-.16,.073),(-.39,.10),(-.43,.03),(-.43,-.095),(-.38,-.105),(-.35,-.075),(-.22,-.036),(-.21,.005),(-.33,.02),(-.17,.04)],.085,'ivory',.009)
  box(root,'Recoil pad',(.10,.18,.03),(0,-.016,-.428),'rubber',.008);box(root,'Cheek rest',(.088,.032,.145),(0,.11,-.30),'rubber',.006)
 else:
  profile(root,'Shoulder stock',[(-.16,.073),(-.36,.075),(-.43,.04),(-.43,-.115),(-.38,-.12),(-.20,-.055),(-.16,-.01)],.09,'body',.012)
  box(root,'Recoil pad',(.104,.17,.032),(0,-.026,-.435),'rubber',.008);box(root,'Cheek pad',(.10,.035,.13),(0,.09-cheek_drop,-.31),'ivory',.008)
def rail(root,start=-.10,end=.35,y=.115):
 box(root,'Optic rail spine',(.06,.013,end-start),(0,y,(start+end)/2),'steel',.003)
 for z in (start+.025,end-.025):box(root,'Rail receiver pedestal',(.045,.044,.035),(0,y-.024,z),'body',.003)
 n=max(2,int((end-start)/.035))
 for i in range(n):box(root,'Rail lug',(.078,.015,.014),(0,y+.013,start+i*(end-start)/(n-1)),'body',.002)
def barrel(root,end=.77,start=.40,r=.022,mat='steel',x=0,y=.028):
 cyl(root,'Barrel',r,end-start,(x,y,(end+start)/2),mat,'z',24);cyl(root,'Muzzle brake',r*1.35,.055,(x,y,end-.025),'steel','z',24);cyl(root,'Recessed bore',r*.68,.003,(x,y,end+.004),'rubber','z',24)
 for i in range(3):box(root,'Brake port',(.008,.014,.014),(x+r*1.33,y,end-.046+i*.015),'rubber',.001)
def tube(parent,name,outer,inner,length,p,mat='steel',segments=32):
 verts=[]
 for z,r in ((-length/2,outer),(length/2,outer),(-length/2,inner),(length/2,inner)):
  for i in range(segments):
   angle=2*math.pi*i/segments;verts.append(tuple(v((r*math.cos(angle),r*math.sin(angle),z))))
 n=segments;faces=[]
 for i in range(n):
  j=(i+1)%n
  faces.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);assign(o,name,mat,parent,p)
 return finish(o,.0008)
def scope(root,z=.055,y=.235,length=.26):
 for zz in (z-.065,z+.065):box(root,'Scope mount',(.05,.05,.03),(0,y-.08,zz),'body',.003);ring(root,'Scope clamp',.041,.007,(0,y,zz),'steel')
 # A real open bore preserves the world view during ADS; no opaque lens discs.
 tube(root,'Open optical housing',.036,.029,length,(0,y,z),'body')
 tube(root,'Open objective housing',.049,.037,.075,(0,y,z+length/2-.025),'steel')
 tube(root,'Open eyepiece',.044,.032,.05,(0,y,z-length/2),'rubber')
 cyl(root,'Elevation turret',.026,.026,(0,y+.047,z),'steel','y',16);cyl(root,'Windage turret',.023,.027,(.046,y,z),'steel','x',16)
def magazine(root,p=(0,-.092,.12),style='curved'):
 mag=group('magazine',root,p)
 if style=='box':
  box(mag,'Belt ammunition box',(.18,.19,.19),(-.043,-.075,0),'ochre',.018);box(mag,'Ammo box lid',(.193,.026,.202),(-.043,.027,0),'steel',.003)
  for i in range(4):box(mag,'Box emboss',(.189,.017,.126),(-.043,-.14+i*.035,0),'body',.004)
 else:
  outline=[(-.05,.025),(.05,.025),(.06,-.065),(.105,-.17),(.026,-.205),(-.02,-.11)] if style=='curved' else [(-.045,.025),(.045,.025),(.045,-.155),(-.045,-.155)]
  profile(mag,'Magazine shell',outline,.066,'body',.005)
  for x in (-.035,.035):
   for i in range(4):box(mag,'Magazine rib',(.006,.011,.07),(x,-.03-i*.034,.006+i*.009 if style=='curved' else 0),'steel',.002)
 return mag
def handle(root,p=(.075,.065,.06)):
 g=group('charging_handle',root,p);box(g,'Bolt carriage',(.025,.028,.072),(0,0,0),'steel',.004);box(g,'Charging latch',(.049,.02,.026),(.025,0,-.02),'rubber',.004);marker(g,'bolt',(0,0,0));return g
def panels(root,length=.31):
 for s in (-1,1):
  box(root,'Receiver inset',(.006,.047,length),(s*.068,.021,.07),'steel',.004);box(root,'Serial inset',(.009,.019,.076),(s*.072,.019,.058),'rubber',.002)
  for zz in (-.07,.09,.205):screw(root,(s*.074,.056,zz))
  box(root,'Power indicator',(.008,.009,.032),(s*.072,.075,.04),'cyan',.001)
WEAPONS=[('weapon_02','burst-rifle'),('weapon_03','heavy-rifle'),('weapon_04','smg'),('weapon_05','double-barrel'),('weapon_06','dmr'),('weapon_07','bolt-sniper'),('weapon_08','lmg'),('weapon_09','grenade-launcher'),('weapon_10','charge-rifle'),('weapon_11','phase-marker')]
RELOAD_RIGS={
 'weapon_05':{'revision':'0.5.2','type':'break-action','hinge':'break_action','pivot':[0,-.027,.146],'axis':[1,0,0],'suggestedOpenDegrees':35,'supply':'magazine','supplyExitDirection':[0,0,-1],'grip':'MagazineGrip','rounds':['shell_L','shell_R']},
 'weapon_08':{'revision':'0.5.2','type':'belt-box','hinge':'feed_cover','pivot':[0,.118,-.145],'axis':[1,0,0],'suggestedOpenDegrees':-55,'supply':'magazine','supplyExitDirection':[0,-1,0],'grip':'MagazineGrip','belt':'ammo_belt','coverGrip':'FeedCoverGrip'},
 'weapon_09':{'revision':'0.5.2','type':'swing-cylinder','hinge':'cylinder_hinge','pivot':[-.09,.02,.03],'axis':[0,0,1],'suggestedOpenDegrees':-65,'cylinder':'magazine','supply':'reload_rounds','supplyExitDirection':[0,0,-1],'grip':'MagazineGrip','chambers':4}
}
def weapon(asset,kind):
 root=group(asset);body=group('receiver',root);grip(body);end=.78;ay=.177;az=.38;pivots=[];front_part=body
 if kind=='burst-rifle':
  profile(body,'Bullpup receiver',[(-.37,-.06),(-.37,.09),(.25,.10),(.42,.025),(.37,-.053),(.10,-.07)],.129,'ivory',.011);box(body,'Bullpup recoil pad',(.14,.18,.035),(0,.012,-.395),'rubber',.011);magazine(root,(0,-.074,-.235));end=.65;barrel(body,end,.39)
  for s in (-1,1):
   box(body,'Perforated handguard',(.018,.105,.22),(s*.074,.02,.295),'body',.007)
   for j in range(4):box(body,'Cooling window',(.021,.034,.027),(s*.078,.025,.22+j*.043),'rubber',.002)
  profile(body,'Carry handle',[(-.12,.10),(-.10,.205),(.24,.205),(.26,.10),(.23,.10),(.21,.172),(-.071,.172),(-.088,.10)],.047,'body',.004);rail(body,-.08,.20,.217);ay=.254;panels(body,.24);handle(root,(.077,.055,-.18))
 elif kind=='heavy-rifle':
  profile(body,'Forged receiver',[(-.16,-.069),(-.18,.09),(.22,.10),(.28,.035),(.22,-.066)],.142,'body',.008);stock(body);magazine(root,(0,-.078,.105),'straight');panels(body,.28);box(body,'Heavy heatshield',(.126,.122,.34),(0,.028,.415),'ivory',.012)
  for s in (-1,1):
   for i in range(7):box(body,'Handguard vent',(.014,.044,.021),(s*.066,.04,.285+i*.041),'rubber',.003)
  end=.96;barrel(body,end,.56,.027);rail(body,-.11,.51);handle(root)
 elif kind=='smg':
  profile(body,'Compact upper',[(-.17,-.045),(-.17,.08),(.20,.08),(.26,.035),(.24,-.05)],.115,'body',.009);stock(body,'wire');end=.47;barrel(body,end,.26,.023);panels(body,.20)
  mag=group('magazine',root,(0,.109,.048));cyl(mag,'Top helical cartridge',.043,.33,(0,0,0),'ivory')
  for i in range(8):ring(mag,'Helical case rib',.044,.002,(0,0,-.12+i*.035),'steel')
  box(body,'Vertical foregrip',(.055,.155,.055),(0,-.105,.205),'rubber',.01);handle(root,(.067,.06,.02));ay=.19;az=.27
 elif kind=='double-barrel':
  profile(body,'Break-action breech',[(-.13,-.053),(-.13,.084),(.145,.084),(.145,-.01),(.12,-.056)],.145,'steel',.007);stock(body,cheek_drop=.025);end=.89
  front_part=group('break_action',root);pivots.append((front_part,RELOAD_RIGS[asset]['pivot']))
  for x in (-.042,.042):barrel(front_part,end,.16,.036,x=x)
  box(front_part,'Joining rib',(.021,.032,.61),(0,.055,.54),'body',.003);profile(front_part,'Contoured fore-end',[(.25,-.015),(.64,-.015),(.68,-.05),(.58,-.089),(.25,-.075)],.10,'ochre',.012)
  for i in range(6):box(front_part,'Fore-end checkering',(.108,.012,.014),(0,-.05,.30+i*.043),'rubber',.002)
  mag=group('magazine',front_part,(0,.015,.15))
  for side,x in (('L',-.042),('R',.042)):
   shell=group('shell_'+side,mag,(x,.013,0))
   cyl(shell,'Shell hull',.025,.075,(0,0,.029),'ochre','z',24)
   cyl(shell,'Shell brass base',.026,.022,(0,0,-.006),'gold','z',24)
   cyl(shell,'Shell rim',.028,.006,(0,0,-.019),'gold','z',24)
   cyl(shell,'Shell primer',.007,.002,(0,0,-.023),'steel','z',16)
   cyl(shell,'Shell crimp',.024,.003,(0,0,.068),'rubber','z',24)
  marker(mag,'MagazineGrip',(-.042,.041,-.019))
  handle(root,(.037,.108,.02));cyl(body,'Breech hinge',.045,.17,(0,-.027,.146),'body','x');az=.72;ay=.106
 elif kind=='dmr':
  profile(body,'Precision receiver',[(-.15,-.055),(-.15,.08),(.25,.08),(.30,.025),(.22,-.057)],.126,'teal',.008);stock(body,'skeleton');magazine(root,(0,-.086,.12),'straight');end=1.02;barrel(body,end,.42,.024)
  box(body,'Octagonal handguard',(.11,.112,.32),(0,.022,.42),'body',.014)
  for s in (-1,1):
   for i in range(5):box(body,'Keyway',(.014,.028,.04),(s*.058,.035,.285+i*.058),'rubber',.003)
  rail(body,-.11,.28);scope(root,.075,.246,.31);handle(root);ay=.246;az=.22;cyl(body,'Suppressor',.041,.20,(0,.028,.945),'body');end=1.048;cyl(body,'Suppressor bore',.015,.002,(0,.028,end),'rubber')
 elif kind=='bolt-sniper':
  cyl(body,'Bolt-action receiver',.057,.35,(0,.023,.01),'steel');stock(body,'skeleton');profile(body,'Precision chassis',[(-.19,-.015),(.36,-.015),(.36,-.078),(.14,-.078),(.08,-.12),(-.16,-.09)],.113,'ivory',.008);magazine(root,(0,-.085,.075),'straight');end=1.18;barrel(body,end,.27,.027)
  for i in range(6):ring(body,'Radiator',.032,.004,(0,.028,.32+i*.046),'body')
  rail(body,-.15,.19);scope(root,.048,.255,.40);ay=.255;az=.23
  bolt=handle(root,(.067,.047,-.08));link(bolt,'Bolt handle',(0,0,0),(.06,-.05,0),.012,'steel');sphere(bolt,'Bolt knob',.025,(.068,-.057,0),'rubber')
  for s in (-1,1):link(body,'Folded bipod',(s*.055,-.025,.67),(s*.063,-.042,.39),.016,'steel')
 elif kind=='lmg':
  profile(body,'Belt-fed receiver',[(-.17,-.07),(-.17,.12),(.26,.12),(.33,.026),(.28,-.075)],.164,'body',.009);stock(body);magazine(root,(-.02,-.084,.06),'box');end=1.01;barrel(body,end,.49,.029);box(body,'Quick-change heatshield',(.145,.143,.37),(0,.028,.43),'ochre',.011)
  for s in (-1,1):
   for i in range(6):cyl(body,'Heatshield perforation',.014,.016,(s*.076,.048,.285+i*.053),'rubber','x',12)
  feed_cover=group('feed_cover',root);pivots.append((feed_cover,RELOAD_RIGS[asset]['pivot']))
  box(feed_cover,'Hinged feed cover',(.178,.024,.305),(0,.132,.015),'steel',.006)
  profile(feed_cover,'Carry handle',[(-.13,.12),(-.10,.215),(.11,.215),(.15,.12),(.11,.12),(.08,.182),(-.07,.182),(-.085,.12)],.033,'rubber',.004)
  marker(feed_cover,'FeedCoverGrip',(0,.215,.015))
  cyl(body,'Feed cover hinge',.018,.19,(0,.118,-.145),'steel','x',20)
  box(body,'Feed tray inset',(.13,.006,.19),(0,.119,.025),'rubber',.002)
  belt=group('ammo_belt',root);pivots.append((belt,(-.09,.075,.025)))
  for i in range(7):
   cyl(belt,'Linked cartridge',.01,.075,(-.09-i*.018,.075-i*.007,.025),'gold','z',12);box(belt,'Ammunition link',(.013,.025,.012),(-.09-i*.018,.075-i*.007,.03),'steel',.002)
  marker(belt,'BeltFeedPoint',(-.09,.075,.025))
  box_mag=next(o for o in root.children if o.name=='magazine');marker(box_mag,'MagazineGrip',(-.1375,-.04,0))
  for s in (-1,1):link(body,'Folded bipod',(s*.071,-.018,.70),(s*.086,-.077,.45),.014,'steel')
  handle(root,(.095,.04,.03));ay=.252;az=.65
 elif kind=='grenade-launcher':
  profile(body,'Launcher frame',[(-.16,-.06),(-.16,.105),(.42,.105),(.46,.035),(.38,-.025),(.27,-.07)],.115,'ochre',.01);stock(body,'wire');end=.70;barrel(body,end,.32,.065)
  hinge=group('cylinder_hinge',root);pivots.append((hinge,RELOAD_RIGS[asset]['pivot']))
  mag=group('magazine',hinge,(0,-.067,.142))
  tube(mag,'Rotary cylinder outer shell',.132,.126,.22,(0,0,0),'body',32)
  cyl(mag,'Cylinder hub',.054,.224,(0,0,0),'body','z',24)
  rounds=group('reload_rounds',mag)
  for j in range(4):
   angle=j*math.pi/2;xx=.095*math.sin(angle);yy=.095*math.cos(angle)
   chamber=group('chamber_'+str(j+1).zfill(2),mag,(xx,yy,0));tube(chamber,'Open grenade chamber',.037,.031,.225,(0,0,0),'steel',24)
   cartridge=group('round_'+str(j+1).zfill(2),rounds,(xx,yy,0))
   cyl(cartridge,'Grenade case',.028,.175,(0,0,-.024),'gold','z',24)
   cyl(cartridge,'Grenade rim',.030,.008,(0,0,-.118),'gold','z',24)
   cyl(cartridge,'Grenade primer',.008,.002,(0,0,-.124),'steel','z',16)
   sphere(cartridge,'Grenade ogive',.028,(0,0,.085),'ochre',(1,1,.85))
  marker(rounds,'MagazineGrip',(-.095,.030,-.118));marker(rounds,'SpeedLoaderGrip',(-.095,.030,-.118))
  cyl(body,'Cylinder crane hinge',.018,.055,(-.09,.02,.03),'steel','z',20)
  rail(body,-.12,.30,.14);ay=.20;az=.33;handle(root,(.08,.075,-.05))
 elif kind=='charge-rifle':
  profile(body,'Accelerator chassis',[(-.18,-.064),(-.18,.09),(.26,.09),(.31,.025),(.25,-.06)],.14,'ivory',.011);stock(body,'skeleton');end=.90;barrel(body,end,.38,.028,mat='glass')
  for s in (-1,1):
   box(body,'Accelerator rail',(.035,.104,.48),(s*.083,.028,.56),'body',.008);box(body,'Energized rail',(.009,.046,.39),(s*.104,.028,.57),'cyan',.003)
  for zz in (.35,.50,.65,.80):ring(body,'Induction coil',.063,.012,(0,.028,zz),'gold')
  mag=group('magazine',root,(0,-.075,.065));box(mag,'Power cassette',(.089,.183,.126),(0,-.062,0),'body',.01)
  for s in (-1,1):box(mag,'Capacitor indicator',(.008,.095,.047),(s*.049,-.063,0),'cyan',.003)
  rail(body,-.12,.24);panels(body,.27);handle(root);ay=.176;az=.77
 else:
  profile(body,'Phase projector shell',[(-.16,-.065),(-.20,.035),(-.13,.13),(.19,.13),(.27,.045),(.22,-.065)],.155,'teal',.014);stock(body,'wire');end=.57
  for s in (-1,1):
   profile(body,'Emitter fork',[(.19,.082),(.58,.092),(.62,.04),(.54,-.028),(.24,-.015)],.027,'steel',.006,x=s*.091);box(body,'Fork channel',(.01,.025,.24),(s*.11,.035,.41),'cyan',.003)
  sphere(body,'Suspended phase lens',.061,(0,.032,.49),'cyan',(.75,.8,1));ring(body,'Focusing hoop',.105,.013,(0,.032,.44),'body')
  cyl(body,'Targeting dish',.095,.024,(0,.17,.03),'body','y',24);ring(body,'Dish perimeter',.083,.006,(0,.185,.03),'cyan','y')
  mag=group('magazine',root,(0,-.09,.075));cyl(mag,'Phase cell',.039,.19,(0,-.053,0),'glass','y')
  for yy in (-.13,-.04,.022):ring(mag,'Cell lock ring',.045,.006,(0,yy,0),'steel','y')
  handle(root,(.095,.048,.02));ay=.223;az=.40
 if kind=='phase-marker':box(body,'Rear sight pedestal',(.042,.064,.037),(0,.158,-.07),'body',.004)
 if kind not in ('dmr','bolt-sniper'):
  box(body,'Rear aperture base',(.068,.036,.041),(0,ay-.025,-.07),'steel',.003)
  for s in (-1,1):box(body,'Rear aperture post',(.008,.032,.031),(s*.027,ay-.002,-.07),'body',.002)
  box(front_part,'Front sight post',(.017,ay-.03,.02),(0,(ay+.05)/2,az),'body',.003);box(front_part,'Phosphor sight',(.006,.009,.004),(0,ay,az-.012),'cyan',.001)
 for name,p in [('Muzzle',(0,.028,end+.012)),('muzzle',(0,.028,end+.012)),('AimRear',(0,ay,-.07)),('AimFront',(0,ay,az)),('sight',(0,ay,-.07)),('Ejection',(.082,.056,.06)),('GripRight',(0,-.12,-.08)),('GripLeft',(-.045,-.063,.34))]:
  marker(front_part if kind=='double-barrel' and name in ('Muzzle','muzzle','AimFront','Ejection','GripLeft') else root,name,p)
 for part,pivot in pivots:authored_pivot(part,pivot)
 return root
def glove(parent,name,p,left=False):
 root=group(name,parent,p);s=-1 if left else 1;box(root,'Glove palm',(.083,.055,.101),(0,0,0),'rubber',.011)
 profile(root,'Metacarpal shield',[(-.043,.021),(-.031,.043),(.026,.043),(.047,.023),(.034,.018),(-.028,.018)],.073,'ivory',.005)
 for i in range(4):
  x=(i-1.5)*.020;length=.037-abs(i-1.4)*.004
  link(root,'Finger proximal',(x,.007,.048),(x,-.012,.048+length),.0095);sphere(root,'Finger joint',.010,(x,-.014,.048+length),'body')
  link(root,'Finger middle',(x,-.014,.048+length),(x,-.043,.070+length),.0085);link(root,'Finger distal',(x,-.043,.070+length),(x,-.051,.052+length),.008)
  box(root,'Knuckle armour',(.016,.011,.019),(x,.021,.055),'steel',.003)
 link(root,'Thumb base',(s*.044,-.005,.004),(s*.062,-.024,.039),.012);link(root,'Thumb tip',(s*.062,-.024,.039),(s*.043,-.040,.068),.010);return root
def arms():
 root=group('operative-arms-v04')
 for side in ('R','L'):
  left=side=='L';elbow=(-.29,-.36,.11) if left else (.16,-.39,-.31);wrist=(-.062,-.105,.332) if left else (.030,-.133,-.095);palm=(-.045,-.078,.35) if left else (.014,-.105,-.070)
  arm=group('forearm_'+side,root,elbow);delta=Vector(wrist)-Vector(elbow);link(arm,'Forearm sleeve',(0,0,0),delta,.057,'body');sphere(arm,'Elbow pad',.059,(0,0,0),'rubber',(1,1.05,1))
  for t in (.25,.68):
   o=cyl(arm,'Fabric cuff',.063,.030,delta*t,'rubber','y');o.rotation_quaternion=Vector((0,0,1)).rotation_difference(v(delta).normalized())
  mid=delta*.52;q=Vector((0,0,1)).rotation_difference(v(delta).normalized())
  plate=box(arm,'Forearm plate',(.096,.17,.034),mid,'ivory',.010);plate.location+=q@Vector((0,.055,0));plate.rotation_mode='QUATERNION';plate.rotation_quaternion=q
  if left:
   display=box(arm,'Phase display',(.056,.048,.009),delta*.70,'cyan',.004);display.location+=q@Vector((0,.078,0));display.rotation_mode='QUATERNION';display.rotation_quaternion=q
  else:
   for i in range(3):box(arm,'Strap clasp',(.014,.034,.040),mid+Vector((.052,-.04+i*.043,0)),'steel',.003)
  glove(root,'hand_'+side,palm,left);marker(root,'wrist_'+side,wrist);marker(root,'elbow_'+side,elbow)
 return root
def robot(kind):
 root=group('robot-'+kind+'-v04');armor='ivory' if kind=='commander' else 'teal' if kind=='sniper' else 'ochre';torso=group('torso',root,(0,1.11,0))
 box(torso,'Torso structure',(.37,.40,.25),(0,0,0),'body',.032);profile(torso,'Breastplate',[(.114,-.20),(.168,-.11),(.178,.11),(.095,.20),(-.05,.18),(-.08,-.15)],.43,armor,.018)
 for s in (-1,1):
  box(torso,'Chest ribs',(.071,.18,.025),(s*.16,.02,.178),'steel',.007)
  for j in range(3):box(torso,'Chest vent',(.083,.014,.031),(s*.15,-.065+j*.046,.192),'rubber',.002)
 box(torso,'Reactor surround',(.14,.16,.022),(0,.035,.204),'body',.008);cyl(torso,'Reactor iris',.047,.013,(0,.047,.224),'steel');cyl(torso,'Reactor lens',.032,.017,(0,.047,.234),'red')
 box(torso,'Back power pack',(.29,.30,.15),(0,.015,-.18),'body',.017)
 for j in range(5):box(torso,'Back radiator',(.25,.014,.025),(0,-.10+j*.052,-.266),'steel',.002)
 hips=group('hips',root,(0,.80,0));box(hips,'Pelvis frame',(.34,.18,.23),(0,0,0),'body',.018);cyl(hips,'Waist bearing',.105,.085,(0,.125,0),'steel','y');ring(hips,'Waist seal',.11,.012,(0,.107,0),'rubber','y')
 for s in (-1,1):box(hips,'Pelvis armour',(.13,.15,.054),(s*.135,.01,.14),armor,.015)
 cyl(root,'Neck actuator',.056,.10,(0,1.386,0),'steel','y');ring(root,'Neck cable seal',.061,.010,(0,1.36,0),'rubber','y')
 head=group('head',root,(0,1.575,0));box(head,'Helmet core',(.283,.264,.275),(0,0,0),'body',.028);box(head,'Brow armour',(.32,.095,.28),(0,.092,0),armor,.021);box(head,'Jaw armour',(.24,.074,.24),(0,-.11,.028),armor,.013)
 box(head,'Visor bezel',(.294,.102,.034),(0,.016,.146),'steel',.008);box(head,'Recessed visor',(.254,.064,.024),(0,.016,.169),'glass',.005);box(head,'Optical scan line',(.208,.017,.013),(0,.018,.187),'red',.003)
 for s in (-1,1):cyl(head,'Audio sensor hinge',.052,.035,(s*.155,-.012,0),'steel','x')
 if kind=='sniper':
  cyl(head,'Rangefinder',.038,.088,(.09,.03,.222),'body');cyl(head,'Rangefinder lens',.029,.008,(.09,.03,.27),'red');link(head,'Aerial',(-.125,.09,-.045),(-.13,.31,-.05),.005,'steel')
 if kind=='commander':
  for s in (-1,1):box(head,'Command temple fin',(.04,.26,.14),(s*.16,.02,-.065),'ivory',.008)
  box(head,'Command identification band',(.27,.024,.018),(0,.12,.15),'gold',.003)
 for s,side in ((-1,'L'),(1,'R')):
  leg=group('leg_'+side,root,(s*.146,.79,0));sphere(leg,'Hip joint',.077,(0,0,0),'steel');link(leg,'Thigh actuator',(0,-.045,0),(0,-.292,.015),.060,'body');box(leg,'Thigh armour',(.159,.244,.077),(0,-.153,.075),armor,.018)
  knee=group('knee_'+side,leg,(0,-.325,.025));cyl(knee,'Knee axle',.059,.19,(0,0,0),'steel','x');box(knee,'Patella',(.13,.085,.056),(0,-.003,.077),'body',.009)
  lower=group('shin_'+side,knee);link(lower,'Shin piston',(0,-.03,0),(0,-.31,.005),.045,'steel');box(lower,'Shin armour',(.15,.25,.117),(0,-.18,.047),armor,.018);box(lower,'Shin service insert',(.071,.16,.018),(0,-.17,.113),'body',.007)
  cyl(lower,'Ankle bearing',.045,.14,(0,-.3815,.014),'steel','x');box(lower,'Toe boot',(.177,.099,.29),(0,-.4155,.084),'rubber',.022);box(lower,'Foot plate',(.158,.032,.18),(0,-.3505,.118),'steel',.007)
  arm=group('arm_'+side,root,(s*.304,1.286,0));sphere(arm,'Shoulder joint',.076,(0,0,0),'steel');box(arm,'Pauldron',(.19,.155,.243),(s*.018,.021,-.009),armor,.022)
  el=(.04,-.225,.12) if s>0 else (.04,-.25,.17) if kind in ('shield','commander') else (.14,-.215,.23)
  hand=(-.164,-.129,.054) if s>0 else (.034,-.12,.22) if kind in ('shield','commander') else (.265,-.094,.237)
  link(arm,'Upper arm',(s*.023,-.045,.01),el,.051,'body');elbow=group('elbow_'+side,arm,el);cyl(elbow,'Elbow axle',.047,.14,(0,0,0),'steel','x')
  fore=group('forearm_'+side,elbow);link(fore,'Forearm piston',(0,0,0),hand,.041,'steel')
  plate=box(fore,'Forearm armour',(.115,.18,.09),Vector(hand)*.5,armor,.013);plate.rotation_mode='QUATERNION';plate.rotation_quaternion=Vector((0,0,1)).rotation_difference(v(hand).normalized());glove(fore,'hand_'+side,hand,s<0)
 if kind in ('shield','commander'):
  shield=group('shield_visual',root,(-.13,.96,.55));profile(shield,'Riot shield',[(0,-.64),(.12,-.53),(.12,.54),(0,.68),(-.06,.54),(-.06,-.54)],.84,'body',.015);box(shield,'Shield armour',(.72,1.12,.058),(0,.01,.146),armor,.028);box(shield,'Inspection glass',(.38,.15,.022),(0,.32,.183),'glass',.008)
  for s in (-1,1):box(shield,'Shield edge',(.039,1.07,.025),(s*.328,.01,.193),'steel',.007)
  box(shield,'Shield warning strip',(.026,.64,.018),(0,-.15,.188),'red',.003)
 if kind=='commander':
  for s in (-1,1):box(root,'Command shoulder',(.23,.08,.29),(s*.335,1.397,0),'ivory',.017)
 gun=group('weapon_pivot',root,(.18,1.02,.23));rifle=group('integrated_enemy_weapon',gun);rifle.scale=(.7,.7,.7)
 profile(rifle,'Enemy weapon receiver',[(-.15,-.06),(-.15,.083),(.27,.083),(.31,.02),(.24,-.06)],.12,'body',.007);grip(rifle)
 profile(rifle,'Enemy stock',[(-.15,.066),(-.37,.055),(-.39,-.07),(-.32,-.09),(-.15,-.025)],.08,'ivory',.009)
 box(rifle,'Enemy handguard',(.105,.10,.27),(0,.02,.365),'steel',.009)
 for side in (-1,1):
  for j in range(4):box(rifle,'Enemy barrel vent',(.012,.030,.024),(side*.056,.025,.27+j*.047),'rubber',.002)
 muzzle=.90 if kind=='sniper' else .76
 barrel(rifle,muzzle,.49,.022)
 if kind=='sniper':rail(rifle,-.08,.25);scope(rifle,.055,.22,.27)
 else:rail(rifle,-.08,.22,.105)
 mag=magazine(rifle,(0,-.072,.09),'straight');marker(rifle,'EnemyMuzzle',(0,.028,muzzle+.012))
 marker(root,'foot_L',(-.146,.01,.07));marker(root,'foot_R',(.146,.01,.07));return root
def optimize(root):
 for parent in list(root.children_recursive)[::-1]+[root]:
  if parent.type!='EMPTY':continue
  sets={}
  for child in list(parent.children):
   if child.type=='MESH':sets.setdefault(child.data.materials[0].name,[]).append(child)
  for mat,objects in sets.items():
   bpy.ops.object.select_all(action='DESELECT')
   for o in objects:o.select_set(True)
   bpy.context.view_layer.objects.active=objects[0]
   if len(objects)>1:bpy.ops.object.join()
   bpy.context.object.name=parent.name+' / '+mat
def preview(root,asset):
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True;scene.render.resolution_x=1100;scene.render.resolution_y=760;scene.render.resolution_percentage=100
 robot=asset.startswith('robot');arms=asset.startswith('operative');target=Vector((0,-.1,.95 if robot else -.08 if arms else .03))
 points=[o.matrix_world@Vector(c) for o in root.children_recursive if o.type=='MESH' for c in o.bound_box]
 target=sum(points,Vector())/len(points)
 camera_p=target+Vector((3,-3.4,1.45)) if robot else target+Vector((1.1,-1.35,.65)) if arms else target+Vector((1.7,-1.5,.90))
 bpy.ops.object.camera_add(location=camera_p);camera=bpy.context.object;camera.name='Preview Camera';camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=2.30 if robot else .86 if arms else 1.60;scene.camera=camera
 q=camera.rotation_euler.to_quaternion().inverted();pts=[q@(p-target) for p in points]
 width=max(p.x for p in pts)-min(p.x for p in pts);height=max(p.y for p in pts)-min(p.y for p in pts)
 center=q.inverted()@Vector(((max(p.x for p in pts)+min(p.x for p in pts))*.5,(max(p.y for p in pts)+min(p.y for p in pts))*.5,0))
 camera.location+=center;camera.data.ortho_scale=max(width,height*scene.render.resolution_x/scene.render.resolution_y)*1.16
 for name,pos,power,size in [('Key',(2,-2,3),500,3),('Fill',(-2,-1,1.2),220,2),('Rim',(0,2,2.5),450,2)]:
  bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name='Preview '+name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
 scene.world.color=(.08,.10,.12);scene.render.image_settings.file_format='PNG';scene.render.filepath=str(PREVIEW/(asset+'.png'));bpy.ops.render.render(write_still=True)
manifest={'generator':'Blender '+bpy.app.version_string,'version':'0.4.0','units':'meters','author_axes':'X right,Y up,Z forward','blender_mapping':'(x,-z,y); export_yup=True','inputs':'original authored geometry only; no external game files','assets':[]}
parser=argparse.ArgumentParser();parser.add_argument('--only',default='');parser.add_argument('--render',action='store_true');parser.add_argument('--validation-output',type=pathlib.Path,default=HERE)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
args.validation_output.mkdir(parents=True,exist_ok=True);bpy.context.preferences.filepaths.save_version=0
todo=WEAPONS+[('operative-arms-v04','arms')]+[('robot-'+k+'-v04',k) for k in ('assault','sniper','shield','commander')]
if args.only and (OUT/'asset-manifest.json').is_file():
 previous=json.loads((OUT/'asset-manifest.json').read_text(encoding='utf-8'))
 manifest.update({key:value for key,value in previous.items() if key!='assets'})
 manifest['assets']=[item for item in previous.get('assets',[]) if item['id'] not in args.only.split(',')]
for asset,kind in todo:
 if args.only and asset not in args.only.split(','):continue
 setup();root=arms() if kind=='arms' else robot(kind) if asset.startswith('robot') else weapon(asset,kind)
 marker(root,'AxisRight',(1,0,0));marker(root,'AxisUp',(0,1,0));marker(root,'AxisForward',(0,0,1))
 root['author']='SwapHunter original Blender authoring';root['units']='meters';root['asset_id']=asset;root['version']='0.4.0';optimize(root);bpy.context.view_layer.update()
 if asset=='weapon_05':
  rear=next(o for o in root.children_recursive if o.name=='AimRear');front=next(o for o in root.children_recursive if o.name=='AimFront')
  direction=(front.matrix_world.translation-rear.matrix_world.translation).normalized();checks=[]
  distance=(front.matrix_world.translation-rear.matrix_world.translation).length+.55-.035
  for label,offset in [('center',(0,0,0)),('left',(-.003,0,0)),('right',(.003,0,0)),('up',(0,.003,0)),('down',(0,-.003,0))]:
   origin=rear.matrix_world.translation-direction*.55+v(offset);hit=bpy.context.scene.ray_cast(bpy.context.evaluated_depsgraph_get(),origin,direction,distance=distance)[0]
   checks.append({'name':label+'_clear_from_eye','passed':not hit})
  (args.validation_output/'weapon_05-eye-line-after.json').write_text(json.dumps({'asset':asset,'eye_offset_m':.55,'stop_before_front_m':.035,'pad_lowering_m':.025,'passed':all(c['passed'] for c in checks),'checks':checks},indent=2),encoding='utf-8')
  if not all(c['passed'] for c in checks):raise RuntimeError('Double barrel rear-eye clearance failed')
 if asset in ('weapon_06','weapon_07'):
  rear=next(o for o in root.children_recursive if o.name=='AimRear');front=next(o for o in root.children_recursive if o.name=='AimFront')
  direction=(front.matrix_world.translation-rear.matrix_world.translation).normalized();checks=[]
  for label,offset in [('center',(0,0,0)),('left',(-.015,0,0)),('right',(.015,0,0)),('up',(0,.015,0)),('down',(0,-.015,0))]:
   origin=rear.matrix_world.translation-direction*.50+v(offset);hit=bpy.context.scene.ray_cast(bpy.context.evaluated_depsgraph_get(),origin,direction,distance=1.3)[0]
   checks.append({'name':label+'_open','passed':not hit})
  origin=rear.matrix_world.translation-direction*.50+v((.040,0,0));hit=bpy.context.scene.ray_cast(bpy.context.evaluated_depsgraph_get(),origin,direction,distance=1.3)[0]
  checks.append({'name':'rim_remains_solid','passed':hit})
  (args.validation_output/(asset+'-optics-ray.json')).write_text(json.dumps({'asset':asset,'passed':all(x['passed'] for x in checks),'checks':checks},indent=2),encoding='utf-8')
  if not all(x['passed'] for x in checks):raise RuntimeError('Open-bore optic validation failed: '+asset)
 meshes=[o for o in root.children_recursive if o.type=='MESH'];vertices=sum(len(o.data.vertices) for o in meshes);triangles=0;points=[]
 for o in meshes:
  o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles);points.extend([o.matrix_world@Vector(c) for c in o.bound_box])
 bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
 for o in root.children_recursive:o.select_set(True)
 bpy.context.view_layer.objects.active=root;bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(asset+'.blend')),check_existing=False)
 bpy.ops.export_scene.gltf(filepath=str(OUT/(asset+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_apply=True,export_extras=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
 points=[(p.x,p.z,-p.y) for p in points];lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
 entry={'id':asset,'kind':kind,'meshes':len(meshes),'vertices':vertices,'triangles':triangles,'bytes':(OUT/(asset+'.glb')).stat().st_size,'blend_source':(SOURCE/(asset+'.blend')).relative_to(HERE.parent.parent).as_posix(),'bounds':{'min':lo,'max':hi,'size':[hi[i]-lo[i] for i in range(3)]},'nodes':[o.name for o in root.children_recursive if o.type=='EMPTY']}
 if asset in RELOAD_RIGS:entry['reload_rig']=RELOAD_RIGS[asset]
 manifest['assets'].append(entry);print('SWAPHUNTER_ASSET '+json.dumps(entry),flush=True)
 if args.render and asset in ('weapon_02','weapon_07','weapon_09','weapon_11','operative-arms-v04','robot-assault-v04'):preview(root,asset)
manifest['assets'].sort(key=lambda item:[a for a,k in todo].index(item['id']))
(OUT/'asset-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8');(HERE/'asset-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('SWAPHUNTER_BLENDER_COMPLETE '+str(len(manifest['assets'])),flush=True)



