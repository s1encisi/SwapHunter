import bpy,pathlib,json,sys,argparse
p=argparse.ArgumentParser();p.add_argument('--out',required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);out=pathlib.Path(a.out).resolve();out.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene;refs=[]
for strip in scene.sequence_editor.strips:
 value=strip.filepath if strip.type=='MOVIE' else strip.sound.filepath if strip.type=='SOUND' else strip.directory if strip.type=='IMAGE' else None
 if value:
  path=pathlib.Path(bpy.path.abspath(value));refs.append({'name':strip.name,'kind':strip.type,'reference':value,'relative':value.startswith('//'),'exists':path.exists()})
for font in bpy.data.fonts:
 if font.filepath and font.filepath!='<builtin>':refs.append({'name':font.name,'kind':'FONT','reference':font.filepath,'relative':font.filepath.startswith('//'),'exists':pathlib.Path(bpy.path.abspath(font.filepath)).is_file()})
report={'blend':str(pathlib.Path(bpy.data.filepath)),'sceneCount':len(bpy.data.scenes),'frames':scene.frame_end,'references':refs,'renderOutputRelative':scene.render.filepath.startswith('//')}
report['passed']=bool(refs) and all(r['exists'] and r['relative'] for r in refs) and report['renderOutputRelative'] and len(bpy.data.scenes)==1
bpy.context.window.scene=scene;scene.frame_set(960);scene.render.image_settings.media_type='IMAGE';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(out/'moved-project-render.png');bpy.ops.render.render(write_still=True)
(out/'portability-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('PORTABILITY '+json.dumps({'passed':report['passed'],'references':len(refs),'frames':scene.frame_end}),flush=True)
if not report['passed']:raise RuntimeError('Missing or absolute project input reference')
