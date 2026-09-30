import * as THREE from 'three';
import {createTacticalWeapon} from './tactical-weapons-v052.mjs';
import {RoundedBoxGeometry} from 'three/addons/geometries/RoundedBoxGeometry.js';
import {GLTFExporter} from 'three/addons/exporters/GLTFExporter.js';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

// All geometry below is independently authored for SwapHunter. No game asset inputs.
const here=path.dirname(fileURLToPath(import.meta.url));
const out=path.resolve(here,'../../Game/Assets/_SwapHunter/Art/ThreeModels');
await fs.mkdir(out,{recursive:true});
globalThis.FileReader=class {
  readAsArrayBuffer(blob){blob.arrayBuffer().then(value=>{this.result=value;this.onload?.({target:this});this.onloadend?.({target:this});});}
  readAsDataURL(blob){blob.arrayBuffer().then(value=>{this.result=`data:${blob.type};base64,${Buffer.from(value).toString('base64')}`;this.onloadend?.({target:this});});}
};
const mat=(name,color,metalness=.25,roughness=.55,emissive=null)=>new THREE.MeshStandardMaterial({name,color,metalness,roughness,...(emissive?{emissive,emissiveIntensity:.8}:{})});
const M={
  graphite:mat('Graphite polymer','#263441',.10,.70),steel:mat('Brushed titanium','#75858b',.75,.3),darksteel:mat('Gunmetal','#36434b',.65,.38),
  ivory:mat('Ceramic armor','#aeb8b5',.15,.52),teal:mat('Port teal paint','#416b76',.22,.52),orange:mat('Safety ochre','#d49243',.18,.47),
  cyan:mat('Phase emitter','#3de1cb',.15,.25,'#24b4a7'),red:mat('Hostile sensor','#e6624c',.12,.25,'#c33321'),gold:mat('Brass contacts','#c89e60',.75,.32),
  rubber:mat('Grip rubber','#151c23',.05,.9),green:mat('Medical indicator','#5cd697',.1,.38,'#2ba761'),glass:mat('Sensor glass','#173641',.75,.19)
};
function group(name,parent){const g=new THREE.Group();g.name=name;if(parent)parent.add(g);return g;}
function mesh(g,geometry,material,p=[0,0,0],rotation=[0,0,0],name='part'){
  const m=new THREE.Mesh(geometry,material);m.name=name;m.position.set(...p);m.rotation.set(...rotation);g.add(m);return m;
}
function box(g,size,p,material=M.graphite,r=.015,rotation=[0,0,0]){
  const radius=Math.max(.0005,Math.min(r,...size.map(v=>v*.45)));
  return mesh(g,new RoundedBoxGeometry(...size,1,radius),material,p,rotation);
}
function cyl(g,radius,length,p,material=M.steel,axis='y',segments=20){return mesh(g,new THREE.CylinderGeometry(radius,radius,length,segments),material,p,axis==='z'?[Math.PI/2,0,0]:axis==='x'?[0,0,Math.PI/2]:[0,0,0]);}
function sphere(g,r,p,material=M.darksteel){return mesh(g,new THREE.SphereGeometry(r,20,12),material,p);}
function capsule(g,r,length,p,material=M.rubber,rotation=[0,0,0]){return mesh(g,new THREE.CapsuleGeometry(r,length,4,12),material,p,rotation);}
function torus(g,r,t,p,material=M.darksteel,rotation=[0,0,0]){return mesh(g,new THREE.TorusGeometry(r,t,6,24),material,p,rotation);}
function bolt(g,p,axis='z',r=.012){cyl(g,r,.008,p,M.steel,axis,8);}
function marker(g,name,p){const node=new THREE.Object3D();node.name=name;node.position.set(...p);g.add(node);return node;}
function strip(g,p,size,material=M.cyan){box(g,size,p,material,.002);}

// Broad chamfered armor carries silhouette; economical panels serve repeated architecture.
function panel(g,size,p,material=M.graphite,rotation=[0,0,0]) { return mesh(g,new THREE.BoxGeometry(...size),material,p,rotation); }
function armorPlate(g,width,height,depth,p,material=M.ivory,cut=.12) {
  const w=width/2,h=height/2,c=Math.min(width,height)*cut;
  const shape=new THREE.Shape();shape.moveTo(-w+c,-h);shape.lineTo(w-c,-h);shape.lineTo(w,-h+c);shape.lineTo(w,h-c);shape.lineTo(w-c,h);shape.lineTo(-w+c,h);shape.lineTo(-w,h-c);shape.lineTo(-w,-h+c);shape.closePath();
  const geo=new THREE.ExtrudeGeometry(shape,{depth,bevelEnabled:true,bevelSize:.004,bevelThickness:.004,bevelSegments:1,steps:1});geo.translate(0,0,-depth/2);
  return mesh(g,geo,material,p);
}

function optimize(g){
  for(const child of [...g.children])if(child.isGroup)optimize(child);
  const sets=new Map();
  for(const child of [...g.children])if(child.isMesh){child.updateMatrix();let geo=child.geometry.index?child.geometry.toNonIndexed():child.geometry.clone();geo.applyMatrix4(child.matrix);const key=child.material.uuid;if(!sets.has(key))sets.set(key,{material:child.material,geometries:[]});sets.get(key).geometries.push(geo);g.remove(child);}
  for(const {material,geometries}of sets.values()){const merged=mergeGeometries(geometries,false);merged.computeBoundingBox();merged.computeBoundingSphere();const m=new THREE.Mesh(merged,material);m.name=g.name+' / '+material.name;g.add(m);}
}
function weapon(scatter=false,enemy=false){
  if(!enemy)return createTacticalWeapon(scatter);
  const root=group(scatter?'S12 Scattergun':'CX24 Carbine');const body=group('receiver',root);
  box(body,[.125,.145,.39],[0,.015,.065],M.graphite,.028);
  box(body,[.14,.072,.30],[0,.085,.04],M.darksteel,.012);
  box(body,[.105,.06,.33],[0,-.061,.05],M.darksteel,.01);
  box(body,[.075,.14,.27],[0,.005,-.28],M.graphite,.022);
  box(body,[.095,.16,.045],[0,0,-.423],M.rubber,.018);
  cyl(body,.033,.12,[0,.03,-.14],M.darksteel,'z');
  box(body,[.075,.18,.08],[0,-.13,-.085],M.rubber,.017,[.28,0,0]);
  box(body,[.09,.022,.145],[0,-.07,-.028],M.darksteel,.009);
  box(body,[.018,.095,.022],[-.05,-.105,.07],M.darksteel,.005);
  box(body,[.10,.018,.115],[0,-.155,.02],M.darksteel,.007);
  box(body,[.014,.06,.018],[0,-.10,.015],M.steel,.004,[.35,0,0]);
  const fore=group('foregrip',root);
  box(fore,[scatter?.145:.12,.11,.28],[0,.015,.365],scatter?M.rubber:M.darksteel,.025);
  for(let i=0;i<8;i++)box(fore,[.13,.012,.008],[0,-.045,.26+i*.029],M.rubber,.003);
  for(let side of[-1,1]){
    for(let i=0;i<5;i++)box(body,[.008,.022,.033],[side*.065,.035,.25+i*.045],M.rubber,.003);
    strip(body,[side*.067,.038,.065],[.009,.019,.17],scatter?M.orange:M.cyan);
    for(let i=0;i<3;i++)bolt(body,[side*.074,.06,-.04+i*.09],'x',.009);
  }
  for(let i=0;i<11;i++)box(body,[.082,.018,.012],[0,.122,-.085+i*.044],M.darksteel,.003);
  const barrelLength=scatter?.27:.34;
  for(let i=0;i<(scatter?2:1);i++){
    const x=scatter?(i-.5)*.047:0;
    cyl(body,scatter?.025:.021,barrelLength,[x,.035,.57],M.darksteel,'z',24);
    cyl(body,scatter?.034:.030,.075,[x,.035,.715],M.steel,'z',24);
    cyl(body,.016,.006,[x,.035,.756],M.rubber,'z',24);
    for(let j=0;j<4;j++)box(body,[.007,.012,.024],[x+.029,.035,.69+j*.014],M.rubber,.002);
  }
  // Physical front-sight pedestal bridges the fore-end (.070m) and sight post (.1325m).
  box(body,[.045,.070,.048],[0,.100,.41],M.darksteel,.005);
  box(body,[.027,.065,.026],[0,.165,.41],M.darksteel,.005);
  strip(body,[0,.2,.397],[.008,.012,.004],M.cyan);
  const sight=group('rear_sight',root);box(sight,[.075,.027,.052],[0,.15,-.025],M.darksteel,.009);
  for(let side of[-1,1])box(sight,[.012,.032,.035],[side*.031,.171,-.025],M.steel,.004);
  const magazine=group('magazine',root);magazine.position.set(0,-.105,.15);
  box(magazine,[.072,.22,.12],[0,-.07,0],M.graphite,.016,[.12,0,0]);
  for(let side of[-1,1])for(let i=0;i<4;i++)box(magazine,[.006,.012,.095],[side*.038,-.14+i*.045,0],M.steel,.003);
  const charging=group('charging_handle',root);box(charging,[.048,.027,.09],[.075,.068,.105],M.steel,.009);
  // Distinct receiver architecture; original muzzle and grip markers are preserved.
  armorPlate(body,.105,.102,.026,[.071,.008,.085],scatter?M.orange:M.ivory,.18).rotation.y=Math.PI/2;
  for(const side of[-1,1]){
    armorPlate(body,.25,.055,.008,[side*.076,.074,.09],M.darksteel,.18).rotation.y=Math.PI/2;
    panel(body,[.009,.028,.091],[side*.078,.011,.057],M.rubber);
    for(let j=0;j<3;j++)panel(body,[.008,.008,.014],[side*.080,.040,.025+j*.022],M.gold);
  }
  if(scatter){
    for(let j=0;j<4;j++){cyl(body,.017,.087,[-.089,-.015,-.025+j*.048],M.orange,'y',12);cyl(body,.018,.016,[-.089,.025,-.025+j*.048],M.gold,'y',12);}
    box(fore,[.17,.10,.22],[0,.011,.372],M.rubber,.02);
    for(let j=0;j<6;j++)panel(fore,[.176,.017,.014],[0,.011,.28+j*.036],M.darksteel);
    box(body,[.16,.054,.22],[0,.098,.047],M.orange,.013);
    for(let side of[-1,1])panel(body,[.010,.065,.19],[side*.084,.028,.28],M.orange);
  }else{
    for(let side of[-1,1]){box(fore,[.024,.115,.26],[side*.071,.018,.377],M.graphite,.01);for(let j=0;j<4;j++)panel(fore,[.026,.033,.031],[side*.073,.029,.29+j*.054],M.rubber);}
    box(body,[.102,.069,.185],[0,.061,-.292],M.ivory,.017);
    panel(body,[.105,.019,.11],[0,-.009,-.302],M.darksteel);
  }
  marker(root,'Muzzle',[0,.035,.772]);marker(root,'Ejection',[.08,.065,.08]);
  marker(root,'AimRear',[0,.171,-.025]);marker(root,'AimFront',[0,.20,.397]);
  if(enemy)root.scale.setScalar(.7);
  return root;
}
function hand(parent,name,p,rot=[0,0,0]){
  const g=group(name,parent);g.position.set(...p);g.rotation.set(...rot);
  box(g,[.096,.07,.105],[0,0,0],M.rubber,.023);
  box(g,[.083,.018,.078],[0,.037,-.007],M.ivory,.016);
  for(let i=0;i<4;i++){
    const x=(i-1.5)*.022;
    capsule(g,.011,.034,[x,-.003,.068],M.rubber,[Math.PI/2,0,0]);
    capsule(g,.010,.025,[x,-.027,.093],M.rubber,[.4,0,0]);
    sphere(g,.011,[x,-.024,.077],M.darksteel);
  }
  capsule(g,.015,.043,[-.059,-.018,.011],M.rubber,[0,0,-.65]);
  return g;
}
function arms(){
  const root=group('Operative first-person arms');
  hand(root,'hand_R',[.012,-.105,-.07],[.12,0,.1]);
  hand(root,'hand_L',[-.046,-.08,.36],[0,.2,Math.PI*.44]);
  const right=group('forearm_R',root);right.position.set(.07,-.26,-.21);right.rotation.set(-.78,0,-.2);
  capsule(right,.063,.32,[0,0,0],M.graphite);
  box(right,[.1,.20,.033],[0,-.01,-.055],M.ivory,.022);
  for(let y of[-.1,.09])torus(right,.063,.007,[0,y,0],M.darksteel,[Math.PI/2,0,0]);
  const left=group('forearm_L',root);left.position.set(-.17,-.24,.18);left.rotation.set(.9,0,-.5);
  capsule(left,.066,.35,[0,0,0],M.graphite);
  box(left,[.105,.24,.035],[0,0,-.057],M.ivory,.021);
  strip(left,[0,.075,-.078],[.052,.044,.008],M.cyan);
  return root;
}
function robot(kind='assault'){
  const root=group(kind+' Sentinel');const armor=kind==='sniper'?M.teal:kind==='elite'?M.ivory:kind==='player'?M.ivory:M.orange;
  const eye=kind==='player'?M.cyan:M.red;
  const torso=group('torso',root);
  box(torso,[.47,.44,.31],[0,1.09,0],M.darksteel,.065);
  box(torso,[.45,.28,.07],[0,1.17,.171],armor,.045,[.08,0,0]);
  box(torso,[.36,.15,.085],[0,.96,.167],armor,.023,[-.12,0,0]);
  box(torso,[.13,.15,.027],[0,1.14,.216],M.graphite,.018);
  strip(torso,[0,1.17,.234],[.075,.025,.014],eye);
  for(let x of[-.15,.15])for(let y of[1.07,1.24])bolt(torso,[x,y,.216],'z',.012);
  box(torso,[.33,.35,.14],[0,1.1,-.20],M.graphite,.04);
  for(let i=0;i<5;i++)box(torso,[.23,.019,.013],[0,.99+i*.045,-.28],M.steel,.005);
  const hips=group('hips',root);box(hips,[.40,.22,.29],[0,.82,0],M.graphite,.05);
  for(let x of[-.16,.16])box(hips,[.13,.15,.065],[x,.84,.163],armor,.025);
  cyl(torso,.065,.11,[0,1.37,0],M.steel);
  const head=group('head',root);head.position.set(0,1.55,0);
  box(head,[.345,.295,.31],[0,0,0],armor,.06);
  box(head,[.29,.105,.045],[0,.015,.16],M.glass,.025);
  strip(head,[0,.015,.188],[.235,.025,.016],eye);
  box(head,[.22,.052,.035],[0,-.106,.15],M.darksteel,.012);
  for(let x of[-.182,.182])cyl(head,.056,.035,[x,-.02,0],M.darksteel,'x',20);
  cyl(head,.009,.14,[.126,.16,-.07],M.steel);sphere(head,.015,[.126,.233,-.07],eye);
  if(kind==='sniper'){box(head,[.14,.07,.105],[.09,.006,.22],M.darksteel,.014);cyl(head,.027,.022,[.09,.006,.286],eye,'z');}
  for(let sign of[-1,1]){
    const leg=group(sign<0?'leg_L':'leg_R',root);leg.position.set(sign*.155,.78,0);
    capsule(leg,.085,.24,[0,-.13,0],M.graphite);
    box(leg,[.17,.27,.11],[0,-.15,.075],armor,.035);
    cyl(leg,.072,.18,[0,-.345,0],M.steel,'x');
    box(leg,[.17,.285,.19],[0,-.50,.013],armor,.042);
    box(leg,[.11,.09,.043],[0,-.39,.12],M.darksteel,.017);
    box(leg,[.19,.14,.33],[0,-.70,.08],M.rubber,.036);
    box(leg,[.18,.065,.19],[0,-.648,.125],M.darksteel,.023);
    const arm=group(sign<0?'arm_L':'arm_R',root);arm.position.set(sign*.31,1.22,0);
    sphere(arm,.10,[0,0,0],M.darksteel);
    box(arm,[.20,.20,.25],[sign*.025,.034,0],armor,.05);
    capsule(arm,.063,.20,[sign*.045,-.19,.08],M.graphite,[.3,0,sign*.12]);
    box(arm,[.14,.20,.115],[sign*.045,-.31,.15],armor,.027,[-.35,0,0]);
    sphere(arm,.052,[sign*.046,-.38,.19],M.rubber);
  }
  if(kind!=='player'){
    const gun=group('weapon_pivot',root);gun.position.set(.26,1.02,.23);const w=weapon(kind==='shield'||kind==='elite',true);gun.add(w);marker(gun,'EnemyMuzzle',[0,.025,.55]);
  }
  if(kind==='shield'||kind==='elite'){
    const shield=group('shield_visual',root);shield.position.set(-.12,.99,.57);
    box(shield,[1.0,1.38,.12],[0,0,0],M.darksteel,.08);
    box(shield,[.84,1.20,.05],[0,0,.08],armor,.055);
    for(let x of[-.35,.35])box(shield,[.065,1.19,.036],[x,0,.124],M.steel,.014);
    strip(shield,[0,0,.125],[.042,.98,.018],eye);
    for(let x of[-.31,.31])for(let y of[-.49,.49])bolt(shield,[x,y,.145]);
    box(shield,[.55,.15,.02],[0,.43,.122],M.glass,.018);
  }
  // Distinct visual roles keep all existing gameplay transforms and named rig nodes.
  if(kind==='assault'){
    armorPlate(torso,.37,.27,.065,[0,1.18,.206],M.orange,.20);
    for(let x of[-.235,.235]){cyl(torso,.053,.32,[x,1.15,-.14],M.darksteel,'y',12);strip(torso,[x,1.15,-.199],[.032,.15,.018],M.red);}
    for(let side of[-1,1])armorPlate(root,.18,.14,.20,[side*.305,1.30,.016],M.orange,.22);
  }
  if(kind==='sniper'){
    armorPlate(head,.30,.11,.22,[0,.11,.02],M.teal,.25);
    box(torso,[.20,.40,.15],[.19,1.18,-.22],M.teal,.025);
    cyl(torso,.016,.40,[-.21,1.42,-.17],M.steel,'y',8);
    for(let j=0;j<3;j++)panel(torso,[.19,.034,.029],[-.18,1.15-j*.059,.20],M.ivory);
    armorPlate(root,.18,.17,.19,[-.31,1.33,0],M.teal,.24);
  }
  if(kind==='shield'||kind==='elite'){
    armorPlate(torso,.44,.35,.075,[0,1.14,.202],kind==='elite'?M.ivory:M.orange,.16);
    for(let side of[-1,1])armorPlate(root,.23,.23,.26,[side*.315,1.29,0],M.darksteel,.20);
    const s=root.getObjectByName('shield_visual');
    for(let side of[-1,1]){armorPlate(s,.14,.93,.024,[side*.35,-.08,.15],M.ivory,.16);panel(s,[.12,.07,.027],[side*.35,-.33,.17],M.orange,[0,0,-.45]);}
  }
  if(kind==='elite'){
    for(let x of[-.27,.27]){armorPlate(torso,.11,.35,.13,[x,1.20,0],M.ivory,.2);strip(torso,[x,1.18,.078],[.042,.15,.012],M.red);}
    armorPlate(head,.30,.085,.26,[0,.14,-.01],M.orange,.2);
    box(torso,[.32,.34,.12],[0,1.27,-.29],M.ivory,.03);
    for(let x of[-.105,0,.105])strip(torso,[x,1.33,-.36],[.025,.17,.017],M.red);
  }
  return root;
}
function crate(){
  const root=group('Modular cargo container');
  box(root,[3.90,2.28,2.90],[0,1.2,0],M.teal,.075);
  for(let x of[-1.91,1.91])for(let z of[-1.41,1.41]){
    box(root,[.18,2.40,.18],[x,1.2,z],M.darksteel,.025);
    for(let y of[.14,2.26])box(root,[.26,.18,.26],[x,y,z],M.steel,.025);
  }
  for(let z of[-1.48,1.48]){
    for(let i=0;i<9;i++)box(root,[.073,1.89,.075],[-1.55+i*.3875,1.2,z],M.darksteel,.014);
    for(let y of[.16,2.24])box(root,[3.9,.16,.12],[0,y,z],M.steel,.022);
    box(root,[.88,.42,.035],[.85,1.56,z*1.016],M.graphite,.012);
    strip(root,[.85,1.65,z*1.034],[.63,.075,.02],M.cyan);
    for(let i=0;i<4;i++)strip(root,[.59+i*.13,1.44,z*1.034],[.058,.11,.02],M.ivory);
  }
  for(let x of[-1.98,1.98])for(let y of[.7,1.6])box(root,[.035,.14,1.55],[x,y,0],M.steel,.012);
  return root;
}
function deck(){
  const root=group('Deck tile 4m');
  // Repeated deck removes micro-bolts; 108 triangles versus 1160 in the previous mesh.
  panel(root,[3.98,.18,3.98],[0,-.11,0],M.darksteel);
  for(let x of[-.985,.985])for(let z of[-.985,.985])panel(root,[1.94,.024,1.94],[x,-.011,z],M.graphite);
  for(let x of[-1.78,1.78])panel(root,[.11,.009,.26],[x,.006,-1.70],M.steel);
  for(let x of[-.09,.09])panel(root,[.10,.008,.033],[x,.006,-1.86],M.ivory);
  return root;
}
function wall(){
  const root=group('Bulkhead module 4m');box(root,[4,3.4,.52],[0,1.7,0],M.darksteel,.045);
  for(let x of[-1,1])box(root,[1.88,2.76,.11],[x,1.64,-.302],M.teal,.045);
  for(let x of[-1.9,0,1.9])box(root,[.13,3.25,.17],[x,1.7,-.33],M.steel,.015);
  for(let y of[.20,3.17])box(root,[3.94,.15,.18],[0,y,-.34],M.darksteel,.018);
  box(root,[1.20,.55,.05],[.92,1.37,-.39],M.graphite,.015);
  for(let i=0;i<6;i++)box(root,[1.04,.038,.035],[.92,1.18+i*.075,-.428],M.steel,.006);
  strip(root,[-1.73,1.83,-.434],[.044,1.45,.026],M.cyan);return root;
}
function terminal(){
  const root=group('Control console');box(root,[.86,.86,.57],[0,.46,0],M.darksteel,.055);
  box(root,[1.02,.16,.75],[0,.91,-.04],M.graphite,.04);
  const display=group('screen',root);display.position.set(0,1.15,.11);display.rotation.x=-.15;
  box(display,[.94,.53,.095],[0,0,0],M.steel,.03);box(display,[.81,.38,.015],[0,0,-.055],M.glass,.01);
  for(let i=0;i<4;i++)strip(display,[-.11,.11-i*.067,-.067],[.45-i*.065,.014,.008],M.cyan);
  for(let x of[-.26,-.12,.02,.16,.30])box(root,[.085,.018,.07],[x,1.007,-.22],M.steel,.007);
  for(let x of[-.22,.22])bolt(root,[x,.40,-.30]);return root;
}
function beacon(){
  const root=group('Mobile phase beacon');cyl(root,.35,.16,[0,.08,0],M.darksteel,'y',32);
  cyl(root,.27,.25,[0,.25,0],M.steel,'y',24);cyl(root,.17,.28,[0,.51,0],M.darksteel,'y',24);
  sphere(root,.28,[0,.96,0],M.cyan);
  for(let angle of[0,Math.PI/2])torus(root,.40,.022,[0,.96,0],M.steel,[0,angle,0]);
  torus(root,.36,.019,[0,.96,0],M.cyan,[Math.PI/2,0,0]);return root;
}
function supply(){
  const root=group('Recovery supply case');box(root,[.55,.36,.42],[0,.18,0],M.graphite,.05);
  box(root,[.52,.09,.40],[0,.36,0],M.ivory,.025);
  for(let x of[-.20,.20])box(root,[.045,.36,.46],[x,.2,0],M.steel,.009);
  strip(root,[0,.24,-.219],[.22,.057,.015],M.green);strip(root,[0,.24,-.228],[.057,.19,.015],M.green);
  return root;
}
function gate(){
  const root=group('Security shutter');box(root,[6.6,4,.24],[0,2,0],M.darksteel,.05);
  for(let x of[-1.58,1.58])box(root,[3.09,3.8,.12],[x,2,-.15],M.teal,.04);
  for(let y of[.5,1.25,2,2.75,3.5])box(root,[6.28,.12,.055],[0,y,-.235],M.steel,.01);
  strip(root,[0,2.1,-.28],[.09,3.5,.025],M.red);
  for(let x of[-2.7,2.7])strip(root,[x,3.38,-.28],[.65,.11,.025],M.red);return root;
}
const assets=[
  ['cx24-carbine',weapon(false),[.18,.45,1.2]],['s12-scattergun',weapon(true),[.18,.45,1.2]],['operative-arms',arms(),[.55,.65,.8]],
  ['operative-body',robot('player'),[.85,1.8,.62]],['assault-sentinel',robot('assault'),[.85,1.8,1]],['sniper-sentinel',robot('sniper'),[.85,1.8,1]],
  ['shield-sentinel',robot('shield'),[1.1,1.8,1]],['commander-sentinel',robot('elite'),[1.1,1.8,1]],
  ['cargo-container',crate(),[4,2.4,3]],['deck-module',deck(),[4,.2,4]],['bulkhead-module',wall(),[4,3.4,.6]],
  ['control-terminal',terminal(),[1.02,1.45,.8]],['phase-beacon',beacon(),[.84,1.4,.84]],['supply-case',supply(),[.55,.42,.46]],['security-gate',gate(),[6.6,4,.3]]
];
const report={generator:'Three.js '+THREE.REVISION,authoring:'independent procedural hard-surface models; silhouette and material hierarchy revision 0.3',units:'meters',inputs:'source code only; no external game assets',assets:[]};
const onlyArg=process.argv.indexOf('--only');
const selected=new Set(onlyArg>=0?(process.argv[onlyArg+1]||'').split(','):[]);
if(selected.size){try{const previous=JSON.parse(await fs.readFile(path.join(out,'asset-manifest.json'),'utf8'));report.assets=previous.assets.filter(x=>!selected.has(x.name));}catch{}}
for(const [name,root,nominalSize]of assets){
  if(selected.size&&!selected.has(name))continue;
  marker(root,'AxisRight',[1,0,0]);marker(root,'AxisForward',[0,0,1]);
  root.userData={project:'SwapHunter',asset:name,version:root.userData.version??'0.3.0',source:root.userData.source??'Tools/ArtAuthoring/generate-assets.mjs',nominalSize};
  optimize(root);root.updateMatrixWorld(true);
  let triangles=0,meshes=0,vertices=0;root.traverse(node=>{if(node.isMesh){meshes++;vertices+=node.geometry.attributes.position.count;triangles+=node.geometry.index?node.geometry.index.count/3:node.geometry.attributes.position.count/3;}});
  const data=await new GLTFExporter().parseAsync(root,{binary:true,trs:true,onlyVisible:true});
  await fs.writeFile(path.join(out,name+'.glb'),Buffer.from(data));
  const bounds=new THREE.Box3().setFromObject(root),size=bounds.getSize(new THREE.Vector3());
  report.assets.push({name,meshes,triangles,vertices,bytes:data.byteLength,nominalSize,bounds:{min:bounds.min.toArray(),max:bounds.max.toArray(),size:size.toArray()}});
  console.log(`${name}: ${meshes} meshes, ${triangles} triangles, ${data.byteLength} bytes`);
}
report.assets.sort((a,b)=>assets.findIndex(x=>x[0]===a.name)-assets.findIndex(x=>x[0]===b.name));
await fs.writeFile(path.join(out,'asset-manifest.json'),JSON.stringify(report,null,2));
await fs.copyFile(path.join(here,'node_modules/three/LICENSE'),path.join(out,'THREE-LICENSE.txt'));
console.log('Export complete: '+out);

