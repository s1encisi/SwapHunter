"""Native dynamic gameplay footage -> portable Blender VSE project -> H264/AAC trailer."""
import bpy,pathlib,json,sys,argparse,shutil
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
p=argparse.ArgumentParser();p.add_argument('--capture',required=True);p.add_argument('--project',required=True);p.add_argument('--output',required=True);p.add_argument('--encode-clips-only',action='store_true');p.add_argument('--prepare-only',action='store_true');a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
raw=pathlib.Path(a.capture).resolve();project=pathlib.Path(a.project).resolve();output=pathlib.Path(a.output).resolve();project.mkdir(parents=True,exist_ok=True);(project/'clips').mkdir(exist_ok=True)
data=json.loads((raw/'capture-manifest.json').read_text(encoding='utf-8-sig'));assert data['passed'],'Capture must pass before export'
fps=30;W=1920;H=1080
def setup(scene,path,frames):
 scene.render.resolution_x=W;scene.render.resolution_y=H;scene.render.resolution_percentage=100;scene.render.fps=fps;scene.frame_start=1;scene.frame_end=frames;scene.render.use_sequencer=True;scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0;scene.view_settings.gamma=1
 scene.render.image_settings.media_type='VIDEO';scene.render.image_settings.file_format='FFMPEG';scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264';scene.render.ffmpeg.constant_rate_factor='HIGH';scene.render.ffmpeg.ffmpeg_preset='GOOD';scene.render.ffmpeg.audio_codec='AAC';scene.render.ffmpeg.audio_bitrate=192;scene.render.filepath=str(path)
for clip in data['clips']:
 dest=project/'clips'/(clip['id']+'.mp4')
 if dest.exists() and dest.stat().st_size>10000:continue
 scene=bpy.data.scenes.new(clip['id']);bpy.context.window.scene=scene;setup(scene,dest,clip['frames']);editor=scene.sequence_editor_create()
 files=sorted((raw/clip['id']).glob('*.png'));assert len(files)==clip['frames']
 strip=editor.strips.new_image(name=clip['id'],filepath=str(files[0]),channel=1,frame_start=1,fit_method='FIT')
 for f in files[1:]:strip.elements.append(f.name)
 strip.frame_final_end=clip['frames']+1
 bpy.ops.render.render(animation=True);assert dest.exists();print('CLIP_ENCODED '+clip['id'],flush=True)
if a.encode_clips_only:sys.exit(0)
audio=json.loads((project/'audio-mix.json').read_text(encoding='utf-8'));total=round(audio['seconds']*fps)
scene=bpy.data.scenes.new('SwapHunter v0.5 trailer');bpy.context.window.scene=scene;setup(scene,output,total);editor=scene.sequence_editor_create()
for old in list(bpy.data.scenes):
 if old!=scene:bpy.data.scenes.remove(old)
background=editor.strips.new_effect(name='Trailer matte',type='COLOR',channel=1,frame_start=1,length=total);background.color=(.006,.013,.022)
font=bpy.data.fonts.load(str(project/'NotoSansSC-Regular.otf'))
def title(name,text,start,length,channel,size,x,y,color=(.91,.97,.98,1)):
 t=editor.strips.new_effect(name=name,type='TEXT',channel=channel,frame_start=start,length=length);t.text=text;t.font=font;t.font_size=size;t.color=color;t.location=(x,y);t.anchor_x='LEFT';t.anchor_y='CENTER';t.alignment_x='LEFT';t.blend_type='ALPHA_OVER';t.use_shadow=True;return t
frame=1;story=[]
headlines={'01-menu':'FPS × 空间换位 × 高机动','02-shoot-swap':'一次换位，两端后果','03-air-swap':'把交换带到空中','04-launch':'借一次大跳，跨过火线','05-directional-shield':'找准方向，留出四秒窗口','07-cover-combat':'迫近与探身，读懂两种威胁','08-shop':'把资源投入你的下一步','09-boss-elbow':'留意换位目标旁的风险','10-boss-attraction':'用换位挣脱牵引','11-boss-exchange':'优势位置，需要争夺','12-boss-laser':'用换位越过激光','13-boss-wave':'地波到来，离开地面','14-boss-omni':'正对来弹，守住窗口'}
for clip in data['clips']:
 count=clip['frames'];video=editor.strips.new_movie(name=clip['id'],filepath=str(project/'clips'/(clip['id']+'.mp4')),channel=2,frame_start=frame,fit_method='FIT');video.frame_final_end=frame+count
 video.transform.scale_x=video.transform.scale_y=.88;video.transform.offset_y=65
 title('Disclosure '+clip['id'],clip['disclosure'],frame,count,4,21,.060,.030,(.57,.77,.81,1))
 headline=headlines.get(clip['id'],clip['description'])
 if clip['id']=='06-three-point-knife':
  for offset,length,value in [(0,45,'① 投掷，留下刀的位置'),(45,45,'② 换到侧面，重画返回路线'),(90,count-90,'③ 召回，让中间目标进入刀路')]:title('Knife step '+str(offset),value,frame+offset,length,5,32,.060,.079)
 else:title('Chapter '+clip['id'],headline,frame,count,5,32,.060,.079)
 story.append({'id':clip['id'],'startFrame':frame,'endFrame':frame+count-1,'description':clip['description'],'caption':headline,'disclosure':clip['disclosure'],'setup':clip['setup']});frame+=count
end=editor.strips.new_image(name='Original key art',filepath=str(project/'key-art.png'),channel=2,frame_start=frame,fit_method='FIT');end.frame_final_end=total+1
title('Final title','换位猎手',frame,total-frame+1,4,104,.08,.64)
title('Final theme','观察空间 · 改变交火的两端',frame,total-frame+1,5,36,.084,.48,(.37,.95,.94,1))
title('Final description','原创单人 FPS  /  v0.5 系统策划 Demo',frame,total-frame+1,6,25,.084,.38)
sound=editor.strips.new_sound(name='Original game event remix',filepath=str(project/'soundtrack.wav'),channel=8,frame_start=1);sound.frame_final_end=total+1
scene.render.filepath='//render/SwapHunter-v05-trailer.mp4'
bpy.ops.wm.save_as_mainfile(filepath=str(project/'SwapHunter-v05-trailer.blend'),check_existing=False)
for strip in editor.strips:
 if hasattr(strip,'filepath') and strip.filepath:strip.filepath=bpy.path.relpath(strip.filepath)
 if strip.type=='IMAGE':strip.directory=bpy.path.relpath(strip.directory)
 if strip.type=='SOUND':strip.sound.filepath=bpy.path.relpath(strip.sound.filepath)
for f in bpy.data.fonts:
 if f.filepath and f.filepath!='<builtin>':f.filepath=bpy.path.relpath(f.filepath)
bpy.ops.wm.save_as_mainfile(filepath=str(project/'SwapHunter-v05-trailer.blend'),check_existing=False)
(project/'storyboard.json').write_text(json.dumps({'fps':fps,'frames':total,'seconds':total/fps,'chapters':story,'audio':'soundtrack.wav','note':data['kind']},ensure_ascii=False,indent=2),encoding='utf-8')
if not a.prepare_only:
 scene.render.filepath=str(output)
 bpy.ops.render.render(animation=True);assert output.is_file() and output.stat().st_size>100000
 print('TRAILER_ENCODED '+str(output),flush=True)
