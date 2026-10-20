"""Build differentiated weapon Foley from attributed licensed recordings; no gameplay timing changes."""
from pathlib import Path
import numpy as np
from scipy.io import wavfile
from scipy import signal
import hashlib,json,shutil
ROOT=Path(__file__).parent
SRC=ROOT/'audio_sources'
OUT=ROOT/'audio_refined'
OUT.mkdir(exist_ok=True)
RATE=48000
manifest=[]
def read(p):
 sr,a=wavfile.read(p)
 if a.ndim>1:a=a.astype(float).mean(axis=1)
 elif np.issubdtype(a.dtype,np.integer):a=a.astype(float)
 a=a.astype(float);a-=a.mean();a/=max(.00001,np.abs(a).max())
 if sr!=RATE:a=signal.resample_poly(a,RATE,sr)
 return a
def pitch(a,f):return np.interp(np.arange(0,len(a)-1,f),np.arange(len(a)),a)
def filt(a,hz,kind='lowpass'):return signal.sosfilt(signal.butter(2,hz,kind,fs=RATE,output='sos'),a)
def envelope(a,fade=.05):
 a=a.copy();n=min(len(a),int(RATE*.0015));a[:n]*=np.linspace(0,1,n)
 n=min(len(a),int(RATE*fade));a[-n:]*=np.linspace(1,0,n);return a
def transient(a,seconds,start=None):
 if start is None:
  hits=np.flatnonzero(np.abs(a)>.16*np.abs(a).max());start=max(0,int(hits[0])-int(RATE*.002))
 return envelope(a[start:start+int(seconds*RATE)])
def add(out,a,at=0,gain=1):
 index=int(at*RATE);n=min(len(a),len(out)-index)
 if n>0:out[index:index+n]+=a[:n]*gain
def save(name,a,sources):
 a=a-np.mean(a);a=envelope(a);a=a/max(.00001,np.abs(a).max())*.77
 pcm=np.round(a*32767).astype('<i2');p=OUT/(name+'.wav');wavfile.write(p,RATE,pcm)
 manifest.append(dict(id=name,sources=sources,samples=len(a),sampleRate=RATE,seconds=round(len(a)/RATE,3),peak=float(np.abs(a).max()),rms=float(np.sqrt(np.mean(a*a))),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
shots={n:read(next((SRC/'shots').rglob(n+'.wav'))) for n in ['cg1','rifle','shotgun','pistol']}
foley={n:read(SRC/(n+'.wav')) for n in ['gunreload','riflereload','shotguncock']}
# Keep separate magazine removal, seating and chambering events at the existing real mechanical commit points.
def segment(a,lo,hi):
 start=int(lo*len(a));end=int(hi*len(a));b=a[start:end]
 return transient(b,min(.38,len(b)/RATE))
parts={'reload_out':segment(foley['riflereload'],0,.30),'reload_in':segment(foley['riflereload'],.36,.76),
       'reload_bolt':transient(foley['shotguncock'],.34),'equip':segment(foley['gunreload'],.02,.31)}
base=['cg1','shotgun','rifle','cg1','pistol','shotgun','rifle','rifle','cg1','shotgun','rifle','pistol']
ratios=[1.0,.94,1.08,.85,1.18,.82,.94,.75,.90,.68,1.16,1.32]
body=[.15,.29,.12,.28,.06,.31,.23,.42,.24,.36,.17,.05]
lengths=[.38,.57,.42,.48,.28,.60,.52,.72,.45,.65,.58,.32]
for w in range(12):
 for v,suffix in enumerate('abc'):
  x=transient(shots[base[w]],lengths[w]);x=pitch(x,ratios[w]*(1+(v-1)*.018))
  out=np.zeros(int(RATE*(lengths[w]+.22)));add(out,x,0,.70)
  add(out,filt(x,210),.004,body[w]);add(out,filt(x,3100),.054+v*.004,.10)
  add(out,filt(x,1800),.103+v*.003,.045)
  if w in (10,11):
   t=np.arange(len(out))/RATE
   out+=(np.sin(2*np.pi*((210 if w==10 else 620)*t+2100*t*t))*.11+np.sin(2*np.pi*1680*t)*.04)*np.exp(-t*15)
  if w==9:out=filt(out,2300)
  save(f'gun_{w:02d}_fire_{suffix}',out,[base[w]+'.wav'])
 for evt,x in parts.items():
  ratio=.88+(w%4)*.075
  if w in (1,5) and evt=='reload_bolt':x=transient(foley['shotguncock'],.40)
  x=pitch(x,ratio);out=np.zeros(int(RATE*.48));add(out,x,0,.7)
  if evt=='equip':out=filt(out,4800)
  if w in (3,8):add(out,filt(x,500),.035,.25)
  save(f'gun_{w:02d}_{evt}',out,['gunreload.wav' if evt=='equip' else 'shotguncock.wav' if evt=='reload_bolt' else 'riflereload.wav'])
# Original feedback cues: brief low body impact versus brighter double transient and a resolved headshot kill.
rng=np.random.default_rng(20261020)
for name,duration in [('impact',.14),('limb_hit',.12),('head_hit',.23),('head_kill',.34)]:
 t=np.arange(int(RATE*duration))/RATE;noise=rng.normal(0,1,len(t))
 a=filt(noise,900)*np.exp(-t*68)*.55+np.sin(2*np.pi*180*t)*np.exp(-t*40)*.17
 if name.startswith('head'):
  a+=np.sin(2*np.pi*1620*t)*np.exp(-t*30)*.22+np.sin(2*np.pi*2430*t)*np.exp(-t*42)*.12
  s=np.maximum(0,t-.055);a+=(t>=.055)*np.sin(2*np.pi*2050*s)*np.exp(-s*50)*.12
 if name=='head_kill':
  s=np.maximum(0,t-.13);a+=(t>=.13)*np.sin(2*np.pi*980*s)*np.exp(-s*24)*.14
 save(name,a,['Original SwapHunter feedback synthesis'])
(OUT/'manifest.json').write_text(json.dumps({'date':'2026-10-20','clips':manifest},ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'CREDITS.txt').write_text('''SwapHunter weapon audio refinement — 2026-10-20

Gunshots: Michel Baradari / apollo-music.de, “Chaingun, pistol, rifle, shotgun shots”.
Source: https://opengameart.org/content/chaingun-pistol-rifle-shotgun-shots
License: Creative Commons Attribution 3.0 — https://creativecommons.org/licenses/by/3.0/
Original files: cg1.wav, pistol.wav, rifle.wav, shotgun.wav.
Changes: trimming, pitch variation, equalization, layering, short reflections, mono resampling and normalization.

Reload and equip Foley: SpringySpringo, “Gun reload sounds”.
Source: https://opengameart.org/content/gun-reload-sounds
License: CC0 — https://creativecommons.org/publicdomain/zero/1.0/
Original files: gunreload1.wav, assaultriflereload1.wav, shotguncock.wav (airsoft recordings).
Changes: segmented into extraction, insertion, chamber and equip actions, pitch/EQ/level processing.

Head/body hit feedback and energy layers: original procedural sounds authored for this project.
No Counter-Strike audio or proprietary Valve assets are used.
''',encoding='utf-8')
print(json.dumps({'clips':len(manifest),'maxPeak':max(m['peak'] for m in manifest),'folder':str(OUT),'bytes':sum(p.stat().st_size for p in OUT.iterdir())}))
