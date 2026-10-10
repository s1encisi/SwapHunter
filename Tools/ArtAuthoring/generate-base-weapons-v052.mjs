import * as THREE from 'three';
import {GLTFExporter} from 'three/addons/exporters/GLTFExporter.js';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import fs from 'node:fs/promises';
import path from 'node:path';
import {deflateSync} from 'node:zlib';
import {createTacticalWeapon} from './tactical-weapons-v052.mjs';

// Dedicated original-asset exporter. Explicit output directory; never exports the other assets.
const at=process.argv.indexOf('--output-dir');
if(at<0||!process.argv[at+1])throw new Error('Pass --output-dir to a new staging directory');
const out=path.resolve(process.argv[at+1]);await fs.mkdir(out,{recursive:true});
for(const name of ['cx24-carbine.glb','s12-scattergun.glb']){try{await fs.access(path.join(out,name));throw new Error('Preserve existing asset: '+name);}catch(e){if(e.code!=='ENOENT')throw e;}}
globalThis.FileReader=class {
 readAsArrayBuffer(blob){blob.arrayBuffer().then(value=>{this.result=value;this.onload?.({target:this});this.onloadend?.({target:this});});}
 readAsDataURL(blob){blob.arrayBuffer().then(value=>{this.result=`data:${blob.type};base64,${Buffer.from(value).toString('base64')}`;this.onloadend?.({target:this});});}
};
function optimize(g){
 for(const c of [...g.children])if(c.isGroup)optimize(c);
 const byMaterial=new Map();
 for(const c of [...g.children])if(c.isMesh){c.updateMatrix();const geo=c.geometry.index?c.geometry.toNonIndexed():c.geometry.clone();geo.applyMatrix4(c.matrix);if(!byMaterial.has(c.material.uuid))byMaterial.set(c.material.uuid,{material:c.material,geometries:[]});byMaterial.get(c.material.uuid).geometries.push(geo);g.remove(c);}
 for(const {material,geometries} of byMaterial.values()){const geo=mergeGeometries(geometries,false);geo.computeBoundingBox();geo.computeBoundingSphere();const mesh=new THREE.Mesh(geo,material);mesh.name=g.name+' / '+material.name;g.add(mesh);}
}
function boundsIn(node){
 const inverse=node.matrixWorld.clone().invert(),bounds=new THREE.Box3();
 node.traverse(o=>{if(o.isMesh){o.geometry.computeBoundingBox();bounds.union(o.geometry.boundingBox.clone().applyMatrix4(o.matrixWorld).applyMatrix4(inverse));}});
 return {min:bounds.min.toArray(),max:bounds.max.toArray(),size:bounds.getSize(new THREE.Vector3()).toArray()};
}
const crcTable=Array.from({length:256},(_,n)=>{for(let k=0;k<8;k++)n=n&1?0xedb88320^(n>>>1):n>>>1;return n>>>0;});
function pngChunk(type,data){const t=Buffer.from(type),content=Buffer.concat([t,data]);let crc=0xffffffff;for(const b of content)crc=crcTable[(crc^b)&255]^(crc>>>8);const head=Buffer.alloc(4),tail=Buffer.alloc(4);head.writeUInt32BE(data.length);tail.writeUInt32BE((crc^0xffffffff)>>>0);return Buffer.concat([head,content,tail]);}
function png(width,height,pixels){const raw=Buffer.alloc(height*(width*4+1));for(let y=0;y<height;y++)pixels.copy(raw,y*(width*4+1)+1,y*width*4,(y+1)*width*4);const ihdr=Buffer.alloc(13);ihdr.writeUInt32BE(width);ihdr.writeUInt32BE(height,4);ihdr[8]=8;ihdr[9]=6;return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),pngChunk('IHDR',ihdr),pngChunk('IDAT',deflateSync(raw)),pngChunk('IEND',Buffer.alloc(0))]);}
async function preview(root,name,removed=false,inspection=false){
 // Small CPU triangle rasterizer for authoring QA only; these are not Unity screenshots.
 const mag=root.getObjectByName('magazine'),before=mag.position.clone();if(removed)mag.position.y-=.27;
 root.updateMatrixWorld(true);const width=1200,height=720,pixels=Buffer.alloc(width*height*4),depth=new Float32Array(width*height).fill(Infinity);
 for(let i=0;i<pixels.length;i+=4){pixels[i]=224;pixels[i+1]=228;pixels[i+2]=229;pixels[i+3]=255;}
 const halfWidth=removed?.84:.71,halfHeight=halfWidth*height/width;
 const camera=new THREE.OrthographicCamera(-halfWidth,halfWidth,halfHeight,-halfHeight,.1,10);camera.position.set(...(inspection?[1.3,-.55,.8]:[1.55,.70,-.60]));camera.lookAt(0,removed?-.18:-.03,.18);camera.updateMatrixWorld();
 const vp=camera.projectionMatrix.clone().multiply(camera.matrixWorldInverse),light=new THREE.Vector3(.45,.8,-.4).normalize();
 const a=new THREE.Vector3(),b=new THREE.Vector3(),c=new THREE.Vector3(),normal=new THREE.Vector3(),center=new THREE.Vector3();
 root.traverse(o=>{if(!o.isMesh)return;const pos=o.geometry.attributes.position,idx=o.geometry.index,count=idx?idx.count:pos.count;
  for(let j=0;j<count;j+=3){const read=(v,k)=>v.fromBufferAttribute(pos,idx?idx.getX(k):k).applyMatrix4(o.matrixWorld);read(a,j);read(b,j+1);read(c,j+2);normal.crossVectors(b.clone().sub(a),c.clone().sub(a)).normalize();center.copy(a).add(b).add(c).multiplyScalar(1/3);if(normal.dot(camera.position.clone().sub(center))<=0)continue;
   const color=o.material.color.clone().multiplyScalar(.34+.66*Math.max(0,normal.dot(light)));color.convertLinearToSRGB();const rgb=[color.r,color.g,color.b].map(v=>Math.round(Math.min(1,v)*255));
   const screen=[a,b,c].map(v=>{const q=v.clone().applyMatrix4(vp);return [(q.x*.5+.5)*width,(.5-q.y*.5)*height,q.z];});const [p,q,r]=screen;
   const area=(q[1]-r[1])*(p[0]-r[0])+(r[0]-q[0])*(p[1]-r[1]);if(Math.abs(area)<.001)continue;
   const xmin=Math.max(0,Math.floor(Math.min(p[0],q[0],r[0]))),xmax=Math.min(width-1,Math.ceil(Math.max(p[0],q[0],r[0]))),ymin=Math.max(0,Math.floor(Math.min(p[1],q[1],r[1]))),ymax=Math.min(height-1,Math.ceil(Math.max(p[1],q[1],r[1])));
   for(let y=ymin;y<=ymax;y++)for(let x=xmin;x<=xmax;x++){const u=((q[1]-r[1])*(x+.5-r[0])+(r[0]-q[0])*(y+.5-r[1]))/area,v=((r[1]-p[1])*(x+.5-r[0])+(p[0]-r[0])*(y+.5-r[1]))/area,w=1-u-v;if(u<0||v<0||w<0)continue;const z=u*p[2]+v*q[2]+w*r[2],offset=y*width+x;if(z>=depth[offset])continue;depth[offset]=z;pixels[offset*4]=rgb[0];pixels[offset*4+1]=rgb[1];pixels[offset*4+2]=rgb[2];}
  }
 });
 await fs.writeFile(path.join(out,name+(inspection?'-well-inspection':removed?'-magazine-out':'-assembled')+'.png'),png(width,height,pixels));mag.position.copy(before);root.updateMatrixWorld(true);
}
function inspectEmptyWell(root){
 const magazine=root.getObjectByName('magazine'),before=magazine.position.clone();magazine.position.y-=.27;root.updateMatrixWorld(true);
 const ray=new THREE.Raycaster(new THREE.Vector3(0,-.18,.15),new THREE.Vector3(0,1,0));const first=ray.intersectObject(root,true)[0];
 const result={magazineOffsetY:-.27,origin:[0,-.18,.15],firstMesh:first?.object.name,firstPoint:first?.point.toArray(),clearToRecessedRoof:!!first&&first.object.name.startsWith('magazine_well /')&&first.point.y>-.05&&first.point.y<-.018};
 magazine.position.copy(before);root.updateMatrixWorld(true);if(!result.clearToRecessedRoof)throw new Error('Empty magazine well blocked: '+JSON.stringify(result));return result;
}
const report={version:'0.5.2',generator:'Three.js '+THREE.REVISION,source:'Tools/ArtAuthoring/tactical-weapons-v052.mjs',preview:'CPU orthographic mesh authoring preview, not Unity',assets:[]};
for(const [name,scatter] of [['cx24-carbine',false],['s12-scattergun',true]]){
 const root=createTacticalWeapon(scatter);for(const [n,p] of [['AxisRight',[1,0,0]],['AxisForward',[0,0,1]]]){const marker=new THREE.Object3D();marker.name=n;marker.position.set(...p);root.add(marker);}
 root.userData.asset=name;root.userData.project='SwapHunter';optimize(root);root.updateMatrixWorld(true);
 const nodes={},materials=[];let meshes=0,triangles=0;
 root.traverse(o=>{if(o.isMesh){meshes++;triangles+=(o.geometry.index?o.geometry.index.count:o.geometry.attributes.position.count)/3;if(!materials.some(m=>m.name===o.material.name))materials.push({name:o.material.name,color:'#'+o.material.color.getHexString(),metalness:o.material.metalness,roughness:o.material.roughness});}});
 for(const n of ['Muzzle','Ejection','AimRear','AimFront','RightGrip','foregrip','SupportGrip','magazine','MagazineGrip','charging_handle']){const o=root.getObjectByName(n);if(!o)throw new Error('Missing contract node '+n);nodes[n]={parent:o.parent.name,localPosition:o.position.toArray(),worldPosition:o.getWorldPosition(new THREE.Vector3()).toArray(),rotation:o.rotation.toArray().slice(0,3)};}
 const buffer=await new GLTFExporter().parseAsync(root,{binary:true,trs:true,onlyVisible:true});await fs.writeFile(path.join(out,name+'.glb'),Buffer.from(buffer));
 report.assets.push({name,bytes:buffer.byteLength,meshes,triangles,vertices:triangles*3,bounds:boundsIn(root),magazineLocalBounds:boundsIn(root.getObjectByName('magazine')),emptyWellInspection:inspectEmptyWell(root),nodes,materials});
 await preview(root,name);await preview(root,name,true);await preview(root,name,true,true);
}
await fs.writeFile(path.join(out,'base-weapons-v052-manifest.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({output:out,assets:report.assets.map(a=>({name:a.name,bytes:a.bytes,meshes:a.meshes,triangles:a.triangles,bounds:a.bounds,magazineLocalBounds:a.magazineLocalBounds,nodes:a.nodes}))},null,2));
