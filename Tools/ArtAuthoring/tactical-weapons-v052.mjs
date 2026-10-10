import * as THREE from 'three';
import {RoundedBoxGeometry} from 'three/addons/geometries/RoundedBoxGeometry.js';

// Original game-prop geometry. Authoring metres, +Z muzzle, +Y up.
// The Unity importer supplies its existing X coordinate correction.
const material=(name,color,metalness,roughness)=>new THREE.MeshStandardMaterial({name,color,metalness,roughness});
const M={
  alloy:material('V052 matte anodized alloy','#343b3e',.78,.56),
  barrel:material('V052 parkerized steel','#252b2e',.85,.53),
  polymer:material('V052 charcoal reinforced polymer','#292e30',.05,.86),
  magazine:material('V052 molded magazine polymer','#3b413e',.08,.81),
  rubber:material('V052 black elastomer','#181b1e',.02,.94),
  steel:material('V052 subdued bolt steel','#727b7e',.88,.43),
  recess:material('V052 recessed steel','#101619',.42,.88),
  marking:material('V052 warm gray index paint','#aaa99b',.01,.84),
};
function group(name,parent){const g=new THREE.Group();g.name=name;if(parent)parent.add(g);return g;}
function part(g,name,geometry,mat,p=[0,0,0],rotation=[0,0,0]){const m=new THREE.Mesh(geometry,mat);m.name=name;m.position.set(...p);m.rotation.set(...rotation);g.add(m);return m;}
function box(g,name,size,p,mat=M.alloy,r=.0025,rotation=[0,0,0]){return part(g,name,new RoundedBoxGeometry(...size,1,Math.min(r,...size.map(v=>v*.35))),mat,p,rotation);}
function cylinder(g,name,r,length,p,mat=M.barrel,axis='z',segments=20){return part(g,name,new THREE.CylinderGeometry(r,r,length,segments),mat,p,axis==='z'?[Math.PI/2,0,0]:axis==='x'?[0,0,Math.PI/2]:[0,0,0]);}
function ring(g,name,r,t,p,mat=M.barrel){return part(g,name,new THREE.TorusGeometry(r,t,6,24),mat,p);}
function mark(g,name,p){const n=new THREE.Object3D();n.name=name;n.position.set(...p);g.add(n);return n;}
function profile(g,name,zy,width,mat=M.polymer,bevel=.0015){
  const shape=new THREE.Shape();shape.moveTo(-zy[0][0],zy[0][1]);for(const [z,y] of zy.slice(1))shape.lineTo(-z,y);shape.closePath();
  const geo=new THREE.ExtrudeGeometry(shape,{depth:width,bevelEnabled:bevel>0,bevelSize:bevel,bevelThickness:bevel,bevelSegments:1,steps:1});
  geo.translate(0,0,-width/2);geo.rotateY(Math.PI/2);return part(g,name,geo,mat);
}
function pin(g,x,y,z,r=.0035){cylinder(g,'Receiver pin',r,.003,[x,y,z],M.steel,'x',12);}

function stock(root,scatter){
  const g=group('stock',root);
  cylinder(g,'Stock extension tube',.018,.215,[0,.022,-.208]);
  box(g,'Stock upper bearing',[.048,.040,.095],[0,.027,-.277],M.polymer,.004);
  box(g,'Narrow cheek piece',[.064,.032,.148],[0,.051,-.298],M.polymer,.004);
  profile(g,'Skeleton lower stock strut',[[-.202,-.008],[-.224,-.040],[-.372,-.076],[-.372,-.052],[-.244,-.018]],.041,M.polymer,.002);
  box(g,'Stock rear frame',[.064,.127,.023],[0,-.005,-.375],M.polymer,.004);
  box(g,'Shoulder recoil pad',[.071,scatter?.142:.132,.023],[0,-.004,-.396],M.rubber,.006);
  for(const y of [-.046,-.023,0,.023,.046])box(g,'Recoil pad texture',[.058,.003,.003],[0,y,-.409],M.polymer,.0005);
  cylinder(g,'Stock latch pivot',.007,.048,[0,-.028,-.259],M.barrel,'x');
  box(g,'Stock release latch',[.036,.009,.040],[0,-.034,-.259],M.rubber,.002,[.18,0,0]);
  for(const x of [-.025,.025])ring(g,'Sling loop',.008,.002,[x,-.031,-.329],M.barrel);
}
function gripAndGuard(root){
  const grip=group('pistol_grip',root);
  profile(grip,'Angled polymer pistol grip',[[-.047,-.069],[-.101,-.063],[-.142,-.221],[-.116,-.244],[-.064,-.231],[-.028,-.101]],.061,M.polymer,.003);
  for(let j=0;j<7;j++)box(grip,'Grip rear traction',[.046,.006,.005],[0,-.101-j*.018,-.092-j*.0045],M.rubber,.001,[.26,0,0]);
  box(grip,'Grip heel cap',[.063,.014,.061],[0,-.232,-.099],M.rubber,.003,[.12,0,0]);
  const guard=group('trigger_guard',root);
  box(guard,'Trigger guard rear',[.037,.080,.010],[0,-.103,-.034],M.alloy,.002);
  box(guard,'Trigger guard forward',[.037,.080,.010],[0,-.103,.047],M.alloy,.002);
  box(guard,'Open trigger guard lower',[.037,.010,.092],[0,-.141,.006],M.alloy,.002);
  cylinder(guard,'Trigger pivot',.008,.039,[0,-.068,.002],M.barrel,'x',12);
  box(guard,'Trigger bow',[.007,.047,.011],[0,-.105,.002],M.barrel,.003,[.33,0,0]);
  mark(root,'RightGrip',[.012,-.105,-.07]);
}
function receiver(root,scatter){
  const body=group('receiver',root);
  // Split side walls form a real ejection-port opening, not a painted bright slab.
  profile(body,'Opposite upper receiver wall',[[-.139,-.010],[-.139,.059],[-.103,.077],[.213,.077],[.257,.044],[.257,-.014]],.009,M.alloy,.0015).position.x=-.046;
  box(body,'Receiver roof',[.092,.014,.355],[0,.070,.050],M.alloy,.002);
  box(body,'Receiver floor',[.082,.023,.368],[0,-.011,.054],M.alloy,.002);
  box(body,'Port upper rim',[.010,.021,.355],[.047,.057,.050],M.alloy,.002);
  box(body,'Port lower rim',[.010,.018,.355],[.047,.006,.050],M.alloy,.002);
  box(body,'Port rear bridge',[.010,.039,.116],[.047,.030,-.071],M.alloy,.002);
  box(body,'Port front bridge',[.010,.039,.089],[.047,.030,.187],M.alloy,.002);
  box(body,'Ejection port shadow',[.006,.037,.151],[.035,.029,.063],M.recess,.001);
  cylinder(body,'Barrel extension collar',.031,.052,[0,.035,.245],M.barrel);
  box(body,'Lower receiver rear',[.075,.044,.142],[0,-.042,-.043],M.polymer,.003);
  for(const x of [-.053,.053]){pin(body,x,.010,-.096);pin(body,x,-.024,.020);pin(body,x,.047,.199);}
  cylinder(body,'Selector spindle',.010,.004,[.054,-.033,-.063],M.barrel,'x',16);
  box(body,'Selector lever',[.006,.011,.027],[.058,-.032,-.059],M.barrel,.002,[.25,0,0]);
  box(body,'Selector index',[.002,.003,.008],[.058,-.014,-.061],M.marking,.0005);
  box(body,'Magazine catch',[.007,.016,.029],[.050,-.063,.116],M.barrel,.002);
  const charging=group('charging_handle',root);
  box(charging,'Bolt carrier visible through port',[.011,.025,.118],[.042,.029,.071],M.steel,.0015);
  cylinder(charging,'Charging handle stem',.005,.025,[.060,.032,.104],M.barrel,'x',12);
  box(charging,'Charging handle knurled head',[.017,.017,.030],[.077,.032,.104],M.barrel,.003);
  for(let j=0;j<3;j++)box(charging,'Handle traction',[.003,.018,.002],[.086,.032,.094+j*.01],M.polymer,.0005);
  return body;
}
function magazine(root,scatter){
  const well=group('magazine_well',root);
  // Four thin walls leave the mouth physically open once magazine is translated -Y.
  const width=scatter?.084:.080,depth=scatter?.142:.136;
  for(const sign of [-1,1])box(well,'Open well side wall',[.008,.087,depth],[sign*(width/2-.004),-.065,.15],M.alloy,.0015);
  for(const sign of [-1,1])box(well,'Open well end wall',[width-.016,.087,.008],[0,-.065,.15+sign*(depth/2-.004)],M.alloy,.0015);
  for(const sign of [-1,1])box(well,'Magazine well flared lip',[.011,.008,depth+.006],[sign*(width/2-.003),-.107,.15],M.polymer,.002);
  // A recessed feed-channel roof is above the opening; there is no solid bottom cap.
  box(well,'Feed channel interior',[width-.018,.005,depth-.020],[0,-.027,.15],M.recess,.0006);
  const mag=group('magazine',root);mag.position.set(0,-.105,.15);
  const outline=scatter?[[-.059,.034],[.059,.034],[.064,-.082],[.072,-.184],[.040,-.205],[-.056,-.196],[-.063,-.069]]:
    [[-.055,.035],[.055,.035],[.060,-.075],[.082,-.180],[.047,-.205],[-.045,-.188],[-.059,-.075]];
  profile(mag,scatter?'Six-round shotgun box magazine':'Curved detachable carbine magazine',outline,scatter?.066:.062,M.magazine,.002);
  const bottom=scatter?-.204:-.202;
  box(mag,'Distinct removable magazine floorplate',[scatter?.077:.073,.015,scatter?.134:.145],[0,bottom,scatter?.007:.014],M.rubber,.0025,[scatter?-.03:-.12,0,0]);
  for(const side of [-1,1]){
    for(let j=0;j<3;j++)box(mag,'Molded longitudinal magazine rib',[.004,scatter?.153:.158,.008],[side*(scatter?.034:.032),scatter?-.094:-.097,-.035+j*.036],M.polymer,.001,[scatter?-.035:-.085,0,0]);
    for(let j=0;j<3;j++)box(mag,'Magazine inspection index',[.0025,.005,.009],[side*(scatter?.036:.034),-.079-j*.033,.033],M.recess,.0005);
  }
  mark(mag,'MagazineGrip',[scatter?.041:.039,-.145,.012]);
  return mag;
}
function rail(g,start,end,y,width=.030){
  box(g,'Narrow rail spine',[width,.012,end-start],[0,y-.006,(start+end)/2],M.barrel,.001);
  const count=Math.floor((end-start)/.024);for(let j=0;j<count;j++)box(g,'Rail recoil lug',[width+.009,.009,.012],[0,y+.003,start+.012+j*.024],M.barrel,.001);
}
function sights(root,body){
  const rear=group('rear_sight',root);
  box(rear,'Rear sight folding foot',[.039,.012,.031],[0,.082,-.025],M.barrel,.002);
  ring(rear,'Open rear aperture',.010,.0025,[0,.105,-.025],M.barrel);
  for(const x of [-.014,.014])box(rear,'Aperture support',[.005,.023,.010],[x,.093,-.025],M.barrel,.001);
  const front=group('front_sight',root);
  box(front,'Front sight foot',[.029,.019,.031],[0,.060,.50],M.barrel,.002);
  box(front,'Fine front sight post',[.0038,.036,.006],[0,.087,.50],M.barrel,.0007);
  for(const x of [-.014,.014])box(front,'Protective front sight ear',[.005,.038,.011],[x,.085,.50],M.barrel,.001);
  box(front,'Low contrast front post index',[.0038,.004,.0065],[0,.103,.4997],M.marking,.0003);
  mark(root,'AimRear',[0,.105,-.025]);mark(root,'AimFront',[0,.105,.50]);
}
function carbineFront(root,body){
  const fore=group('foregrip',root);
  cylinder(body,'Single rifled barrel',.012,.49,[0,.035,.503],M.barrel);
  cylinder(body,'Gas block',.020,.032,[0,.035,.496],M.barrel);
  cylinder(body,'Flash hider body',.021,.051,[0,.035,.741],M.barrel);
  // A ring at the muzzle gives a visible dark bore instead of a flat silver cylinder end.
  ring(body,'Flash hider muzzle rim',.017,.003,[0,.035,.767],M.barrel);
  cylinder(body,'Muzzle bore recess',.014,.008,[0,.035,.761],M.recess);
  for(const x of [-.021,.021])for(let j=0;j<3;j++)box(body,'Flash hider side slot',[.002,.008,.014],[x,.035,.721+j*.015],M.recess,.0005);
  // Separate rails and ribs expose the barrel through the handguard windows.
  box(fore,'Handguard top bridge',[.060,.012,.254],[0,.068,.372],M.alloy,.002);
  box(fore,'Handguard lower web',[.064,.014,.251],[0,-.017,.372],M.alloy,.002);
  for(const side of [-1,1]){
    box(fore,'Handguard upper edge',[.012,.013,.251],[side*.039,.055,.372],M.alloy,.0015);
    box(fore,'Handguard lower edge',[.012,.013,.251],[side*.039,-.006,.372],M.alloy,.0015);
    for(let j=0;j<5;j++)box(fore,'Open-window handguard rib',[.010,.061,.012],[side*.039,.024,.256+j*.059],M.alloy,.0015);
    box(fore,'Short polymer support panel',[.009,.030,.147],[side*.046,.012,.365],M.polymer,.002);
    for(let j=0;j<6;j++)box(fore,'Support panel traction',[.0025,.028,.006],[side*.051,.012,.305+j*.024],M.rubber,.0005);
  }
  rail(body,-.108,.219,.086);rail(fore,.244,.486,.084);
  mark(fore,'SupportGrip',[.056,-.026,.365]);
}
function shotgunFront(root,body){
  cylinder(body,'Single shotgun barrel',.018,.51,[0,.035,.512],M.barrel);
  cylinder(body,'Fixed under-barrel slide guide',.014,.430,[0,-.016,.425],M.barrel);
  cylinder(body,'Slide guide end cap',.018,.024,[0,-.016,.651],M.barrel);
  ring(body,'Shotgun muzzle rim',.018,.0025,[0,.035,.770],M.barrel);
  cylinder(body,'Shotgun bore recess',.0155,.006,[0,.035,.765],M.recess);
  const fore=group('foregrip',root);
  box(fore,'Independent pump sleeve',[.105,.084,.236],[0,-.017,.362],M.polymer,.008);
  for(let j=0;j<8;j++){
    box(fore,'Pump lower traction',[.098,.006,.011],[0,-.059,.260+j*.029],M.rubber,.001);
    for(const side of [-1,1])box(fore,'Pump side traction',[.005,.065,.011],[side*.053,-.014,.260+j*.029],M.rubber,.001);
  }
  for(const side of [-1,1])box(fore,'Pump action bar',[.006,.010,.269],[side*.031,-.022,.223],M.barrel,.001);
  rail(body,-.108,.199,.086);
  mark(fore,'SupportGrip',[.062,-.045,.365]);
}
export function createTacticalWeapon(scatter=false){
  const root=group(scatter?'S12 Tactical Box-fed Pump':'CX24 Tactical Carbine');
  root.userData={version:'0.5.2',source:'Tools/ArtAuthoring/tactical-weapons-v052.mjs',original:true};
  const body=receiver(root,scatter);stock(root,scatter);gripAndGuard(root);magazine(root,scatter);
  if(scatter)shotgunFront(root,body);else carbineFront(root,body);
  sights(root,body);
  mark(root,'Muzzle',[0,.035,.772]);mark(root,'Ejection',[.08,.065,.08]);
  root.updateMatrixWorld(true);return root;
}
