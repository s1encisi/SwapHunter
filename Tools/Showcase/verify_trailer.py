import bpy,aud,numpy as np,pathlib,json,sys,argparse
p=argparse.ArgumentParser();p.add_argument('--video',required=True);p.add_argument('--out',required=True);p.add_argument('--frames',type=int,default=2550);a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
video=pathlib.Path(a.video).resolve();out=pathlib.Path(a.out).resolve();out.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes.new('Actual encoded trailer decode');bpy.context.window.scene=scene;scene.render.resolution_x=1920;scene.render.resolution_y=1080;scene.render.resolution_percentage=100;scene.render.fps=30;scene.render.use_sequencer=True;scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
strip=scene.sequence_editor_create().strips.new_movie(name='Encoded result',filepath=str(video),channel=1,frame_start=1,fit_method='FIT')
sound=aud.Sound(str(video));sample_rate,channels=sound.specs;pcm=np.asarray(sound.data());audio_seconds=len(pcm)/sample_rate
data=video.read_bytes();report={'file':str(video),'encodedBytes':len(data),'decodedFrames':strip.frame_duration,'expectedFrames':a.frames,'fps':getattr(strip,'fps',30),'h264Tag':b'avc1' in data,'aacTag':b'mp4a' in data,'audioSampleRate':sample_rate,'audioChannels':channels,'audioSeconds':audio_seconds,'audioPeak':float(np.abs(pcm).max()),'audioRms':float(np.sqrt(np.mean(pcm**2))),'audioFinite':bool(np.isfinite(pcm).all()),'sampledFrames':[]}
scene.render.image_settings.media_type='IMAGE';scene.render.image_settings.file_format='PNG'
for frame in (1,180,540,960,1200,1350,1470,1491,1550,1650,2010,2370,a.frames-15):
 if frame>a.frames:continue
 bpy.context.window.scene=scene;scene.frame_set(frame);scene.render.filepath=str(out/('decoded-'+str(frame).zfill(4)+'.png'));bpy.ops.render.render(scene=scene.name,write_still=True);report['sampledFrames'].append({'frame':frame,'file':str(scene.render.filepath)})
report['passed']=report['decodedFrames']==a.frames and abs(audio_seconds-a.frames/30)<.2 and report['audioPeak']>.005 and report['audioPeak']<=1.001 and report['audioFinite'] and report['h264Tag'] and report['aacTag']
(out/'video-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('VIDEO_VERIFIED '+json.dumps({k:v for k,v in report.items() if k!='sampledFrames'}),flush=True)
if not report['passed']:raise RuntimeError('Trailer verification failed')
