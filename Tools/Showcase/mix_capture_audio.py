"""Reconstruct original game audio from captured playback events, not a claimed loopback recording."""
import argparse,json,pathlib,wave
import numpy as np
p=argparse.ArgumentParser();p.add_argument('--capture',required=True);p.add_argument('--out',required=True);a=p.parse_args()
capture=pathlib.Path(a.capture);out=pathlib.Path(a.out);out.mkdir(parents=True,exist_ok=True)
manifest=json.loads((capture/'capture-manifest.json').read_text(encoding='utf-8-sig'))
clips=manifest['clips'];rate=48000;duration=sum(c['frames']/c['fps'] for c in clips)+3
mix=np.zeros((round(duration*rate),2),np.float32);cache={}
def load(name):
 if name not in cache:
  with wave.open(str(capture/'audio'/(name+'.wav')),'rb') as f:
   assert f.getframerate()==rate and f.getsampwidth()==2
   values=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype(np.float32)/32768
   cache[name]=values.reshape(-1,f.getnchannels()).mean(axis=1)
 return cache[name]
music=load('phase_score');bed=load('port_ambience');n=len(mix)
mix[:]+=np.resize(music,n)[:,None]*.55;mix[:]+=np.resize(bed,n)[:,None]*.07
offset=0;events=0;chapters=[]
for clip in clips:
 seconds=clip['frames']/clip['fps'];chapters.append({'id':clip['id'],'start':offset,'seconds':seconds,'description':clip['description'],'disclosure':clip['disclosure'],'setup':clip['setup']})
 for ev in clip['sounds']:
  if ev['seconds']<0 or ev['seconds']>=seconds:continue
  source=load(ev['id']);positions=np.arange(0,len(source),max(.1,ev['pitch']));signal=np.interp(positions,np.arange(len(source)),source).astype(np.float32)
  count=min(len(signal),round((seconds-ev['seconds'])*rate));signal=signal[:count]*ev['volume']
  at=round((offset+ev['seconds'])*rate);count=min(count,len(mix)-at)
  if count<=0:continue
  pan=np.clip(ev['pan'],-1,1);mix[at:at+count,0]+=signal[:count]*np.sqrt((1-pan)/2);mix[at:at+count,1]+=signal[:count]*np.sqrt((1+pan)/2);events+=1
 offset+=seconds
fade=min(24000,n//2);mix[:fade]*=np.linspace(0,1,fade)[:,None];mix[-fade:]*=np.linspace(1,0,fade)[:,None]
raw_peak=float(np.max(np.abs(mix)));raw_rms=float(np.sqrt(np.mean(mix**2)));gain=min(5,.065/max(.001,raw_rms));mix*=gain
# Gentle peak limiting preserves gun transients without hard clipping; this is RMS, not a LUFS claim.
magnitude=np.abs(mix);mix=np.sign(mix)*np.where(magnitude>.7,.7+.24*np.tanh((magnitude-.7)/.24),magnitude)
with wave.open(str(out/'soundtrack.wav'),'wb') as f:f.setnchannels(2);f.setsampwidth(2);f.setframerate(rate);f.writeframes((np.clip(mix,-1,1)*32767).astype('<i2').tobytes())
report={'method':'Original game WAV clips reconstructed using actual emitted sound event times, pitch and spatial pan; continuous original PhaseScore music rebalanced for the trailer, RMS-normalized with gentle peak limiting. Not a hardware-loopback recording or a LUFS measurement.','seconds':duration,'sampleRate':rate,'channels':2,'events':events,'rawPeak':raw_peak,'rawRms':raw_rms,'masterGain':gain,'peak':float(np.max(np.abs(mix))),'rms':float(np.sqrt(np.mean(mix**2))),'finite':bool(np.isfinite(mix).all()),'chapters':chapters}
(out/'audio-mix.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in report.items() if k!='chapters'}))
