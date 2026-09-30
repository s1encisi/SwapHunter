"""Blender VSE: truthful screenshot showcase, no fake continuous gameplay.
Usage: blender --background --factory-startup --python-exit-code 1 --python make_showcase.py --
 --shots DIRECTORY --lineup PNG --output MP4 [--audio WAV] [--weapon-shots DIRECTORY]
 [--storyboard JSON] [--draft] [--prepare-only]
"""
import bpy, pathlib, argparse, sys, json, math, struct
HERE=pathlib.Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument('--shots',required=True);parser.add_argument('--lineup',required=True);parser.add_argument('--output',required=True)
parser.add_argument('--audio');parser.add_argument('--weapon-shots');parser.add_argument('--storyboard')
parser.add_argument('--draft',action='store_true');parser.add_argument('--prepare-only',action='store_true')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
shots=pathlib.Path(args.shots).resolve();out=pathlib.Path(args.output).resolve();out.parent.mkdir(parents=True,exist_ok=True)
lineup=pathlib.Path(args.lineup).resolve();audio=pathlib.Path(args.audio).resolve() if args.audio else None
weapons=pathlib.Path(args.weapon_shots).resolve() if args.weapon_shots else shots/'weapon-visuals'
font_path=HERE.parent.parent/'Game/Assets/_SwapHunter/Fonts/NotoSansSC-Regular.otf'
fps=24;W,H=1920,1080
entries=[]
def add(file,seconds,title,caption,source='Unity 原生截图'):
 entries.append({'file':str(file),'seconds':seconds,'title':title,'caption':caption,'source':source})
storyboard_base=pathlib.Path.cwd()
if args.storyboard:
 storyboard_file=pathlib.Path(args.storyboard).resolve();storyboard_base=storyboard_file.parent
 entries=json.loads(storyboard_file.read_text(encoding='utf-8-sig'))
else:
 add(shots/'00-action-map.png',3,'十二段相位行动','章节进度、关卡目标与可重复挑战')
 add(shots/'00-weapons.png',3,'双武器装配','12 种武器 · 主副武器槽 · 配件选择')
 add(shots/'00-backpack.png',3,'战术背包','补给数量、任务物品与行动状态')
 add(lineup,3,'原创武器建模','十把新增武器采用不同结构与轮廓','Blender 原创资产预览')
 for name,label in [('weapon-02-hip.png','三连发步枪'),('weapon-09-hip.png','榴弹发射器'),('weapon-11-hip.png','相位标记器')]:
  if (weapons/name).is_file():add(weapons/name,2,label,'Unity 原生武器视图；静态截图不代表完整动作')
 add(shots/'level_01-entry.png',3,'换位与校准','交换位置、保留朝向；画面来自规则验证')
 add(shots/'level_09-entry.png',3,'立体空间与回程','高低差、补给位置与返回入口的路线')
 add(shots/'level_12-entry.png',3,'综合目标与敌人组合','最终关卡的多层空间与撤离目标')
 add(shots/'level_12-settlement.png',4,'行动结算与成长','关卡奖励存入档案，再选择下一次配置')
for entry in entries:
 source_file=pathlib.Path(entry['file'])
 entry['file']=str((source_file if source_file.is_absolute() else storyboard_base/source_file).resolve())
 if not pathlib.Path(entry['file']).is_file():raise FileNotFoundError(entry['file'])
 if not entry.get('source'):entry['source']='Unity 原生截图'
if audio and not audio.is_file():raise FileNotFoundError(str(audio))
seconds=sum(float(e['seconds']) for e in entries)
if not 20<=seconds<=35:raise ValueError('Showcase duration must be 20–35 seconds, got '+str(seconds))
scene=bpy.context.scene
if scene.sequence_editor:scene.sequence_editor_clear()
editor=scene.sequence_editor_create();scene.render.resolution_x=W;scene.render.resolution_y=H;scene.render.resolution_percentage=100
scene.render.fps=fps;scene.frame_start=1;scene.frame_end=round(seconds*fps);scene.render.use_sequencer=True
scene.view_settings.view_transform='Standard'
try:scene.view_settings.look='None'
except:pass
scene.view_settings.exposure=0;scene.view_settings.gamma=1
font=bpy.data.fonts.load(str(font_path))
bg=editor.strips.new_effect(name='Background',type='COLOR',channel=1,frame_start=1,length=scene.frame_end)
bg.color=(.002,.004,.009)
def text(name,value,start,length,channel,size,pos,color=(.87,.92,.97,1),right=False):
 t=editor.strips.new_effect(name=name,type='TEXT',channel=channel,frame_start=start,length=length)
 t.text=value;t.font=font;t.font_size=size;t.color=color;t.location=pos;t.anchor_x='RIGHT' if right else 'LEFT';t.anchor_y='CENTER'
 t.alignment_x='RIGHT' if right else 'LEFT';t.blend_type='ALPHA_OVER';t.use_shadow=False
 return t
text('Project title','SWAPHUNTER / V0.4',1,scene.frame_end,5,30,(.04,.953),(.28,.70,1,1))
text('Disclosure','实机截图与功能展示'+(' · 试片' if args.draft else ''),1,scene.frame_end,6,27,(.96,.953),right=True)
text('Evidence boundary','Unity 原生验证截图与 Blender 资产预览；此剪辑不是一段真人连续游玩录像。',1,scene.frame_end,7,18,(.04,.022),(.58,.69,.79,1))
frame=1
for index,entry in enumerate(entries):
 duration=round(float(entry['seconds'])*fps);entry['start_frame']=frame;entry['end_frame']=frame+duration-1
 img=bpy.data.images.load(entry['file'],check_existing=True);width,height=img.size[:]
 if not width or not height:raise ValueError('Unreadable image '+entry['file'])
 strip=editor.strips.new_image(name='Screenshot '+str(index+1),filepath=entry['file'],channel=2,frame_start=frame,fit_method='ORIGINAL')
 strip.frame_final_end=frame+duration;scale=min(1780/width,820/height)
 strip.transform.scale_x=scale;strip.transform.scale_y=scale;strip.transform.offset_y=16
 strip.blend_type='ALPHA_OVER';strip.blend_alpha=1
 entry['source_pixels']=[width,height];entry['display_scale']=scale
 text('Source '+str(index+1),entry['source'],frame,duration,8,19,(.04,.903),(.58,.69,.79,1))
 text('Chapter '+str(index+1),str(index+1).zfill(2)+' / '+entry['title'],frame,duration,9,35,(.04,.104))
 text('Caption '+str(index+1),entry['caption'],frame,duration,10,25,(.04,.061),(.64,.75,.84,1))
 frame+=duration
audio_entries=[]
if audio:
 offset=1;i=0
 while offset<=scene.frame_end:
  sound=editor.strips.new_sound(name='Original phase score '+str(i+1),filepath=str(audio),channel=20+i,frame_start=offset)
  available=sound.frame_duration
  if available<fps:raise ValueError('Soundtrack shorter than one second')
  end=min(offset+available,scene.frame_end+1);sound.frame_final_end=end
  fade=min(12,max(2,(end-offset)//4))
  sound.volume=0;sound.keyframe_insert(data_path='volume',frame=offset)
  sound.volume=.35;sound.keyframe_insert(data_path='volume',frame=offset+fade)
  sound.volume=.35;sound.keyframe_insert(data_path='volume',frame=end-fade)
  sound.volume=0;sound.keyframe_insert(data_path='volume',frame=end-1)
  audio_entries.append({'start_frame':offset,'end_frame':end-1,'volume':.35})
  if end>=scene.frame_end+1:break
  offset=end-12;i+=1
scene.render.image_settings.media_type='VIDEO';scene.render.image_settings.file_format='FFMPEG';scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264'
scene.render.ffmpeg.constant_rate_factor='MEDIUM';scene.render.ffmpeg.ffmpeg_preset='GOOD';scene.render.ffmpeg.gopsize=48
scene.render.ffmpeg.audio_codec='AAC' if audio else 'NONE';scene.render.ffmpeg.audio_bitrate=192;scene.render.ffmpeg.audio_mixrate=48000
scene.render.filepath=str(out);scene.render.use_file_extension=True
timeline=HERE/'timelines'/(out.stem+'.blend');timeline.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(timeline),check_existing=False)
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_as_mainfile(filepath=str(timeline),check_existing=False)
report={'kind':'Actual Unity screenshots and original Blender asset previews, not continuous human gameplay','draft':args.draft,'blender':bpy.app.version_string,'output':str(out),'timeline':str(timeline),'resolution':[W,H],'fps':fps,'expected_frames':scene.frame_end,'duration_seconds':seconds,'codec_requested':'H264 in MPEG4','soundtrack':str(audio) if audio else None,'audio_segments':audio_entries,'chapters':entries,'rendered':False}
report_path=out.with_suffix('.json');report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
if args.prepare_only:
 print('SHOWCASE_TIMELINE_PREPARED '+str(timeline),flush=True)
else:
 previews=out.parent/(out.stem+'-frames');previews.mkdir(parents=True,exist_ok=True)
 scene.render.image_settings.media_type='IMAGE';scene.render.image_settings.file_format='PNG'
 for f in [1,round(scene.frame_end*.50),scene.frame_end]:
  scene.frame_set(f);scene.render.filepath=str(previews/('layout-'+str(f).zfill(4)+'.png'));bpy.ops.render.render(write_still=True)
 scene.render.image_settings.media_type='VIDEO';scene.render.image_settings.file_format='FFMPEG';scene.render.filepath=str(out);scene.frame_set(1)
 bpy.ops.render.render(animation=True)
 if not out.is_file() or out.stat().st_size<10000:raise RuntimeError('Encoded MP4 missing or empty')
 probe=bpy.data.scenes.new('Encoded MP4 verification');probe.render.resolution_x=W;probe.render.resolution_y=H;probe.render.resolution_percentage=100;probe.render.fps=fps;probe.render.use_sequencer=True;probe.view_settings.view_transform='Standard'
 try:probe.view_settings.look='None'
 except:pass
 movie=probe.sequence_editor_create().strips.new_movie(name='Actual encoded MP4',filepath=str(out),channel=1,frame_start=1,fit_method='FIT')
 report['decoded_frames']=movie.frame_duration;report['encoded_bytes']=out.stat().st_size;report['has_avc1_tag']=b'avc1' in out.read_bytes()
 bpy.context.window.scene=probe;probe.frame_end=scene.frame_end;probe.frame_set(round(scene.frame_end*.5));probe.render.image_settings.media_type='IMAGE';probe.render.image_settings.file_format='PNG';probe.render.filepath=str(previews/'encoded-midpoint.png')
 bpy.ops.render.render(scene=probe.name,write_still=True)
 report['rendered']=True;report['passed']=report['decoded_frames']==scene.frame_end and report['has_avc1_tag']
 report['decoded_frame_png']=probe.render.filepath
 report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 if not report['passed']:raise RuntimeError('Encoded MP4 duration/codec check failed')
 print('SHOWCASE_RENDER_COMPLETE '+json.dumps({'output':str(out),'seconds':seconds,'frames':report['decoded_frames'],'bytes':report['encoded_bytes']},ensure_ascii=False),flush=True)



