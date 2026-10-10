"""Truthful local still-frame mechanism guide using Blender VSE.
Run in Blender --background --factory-startup --python-exit-code 1 --python THIS -- ARGS.
--check-only can also run with ordinary Python and never starts Blender/Unity.
"""
import argparse
import hashlib
import json
import pathlib
import shutil
import struct
import sys

HERE = pathlib.Path(__file__).resolve().parent
p = argparse.ArgumentParser(description="将同一构建的原生离屏PNG制作为45秒中文静帧导览；不生成游戏动作。")
p.add_argument("--shots", required=True, help="VisualV06QA原生PNG目录")
p.add_argument("--identity", required=True, help="该次验证对应的build-identity.json")
p.add_argument("--run-config", help="默认取PNG目录上一层run-config.json")
p.add_argument("--visual-report", help="默认取PNG目录内visual-v06-report.json")
p.add_argument("--font", required=True, help="项目已有NotoSansSC-Regular.otf")
p.add_argument("--font-license", required=True, help="对应已有OFL.txt")
p.add_argument("--output", required=True, help="新的MP4路径；拒绝覆盖已有产物")
p.add_argument("--storyboard", default=str(HERE / "storyboard-v06.json"))
mode = p.add_mutually_exclusive_group()
mode.add_argument("--draft", action="store_true", help="草稿：默认模式，常驻草稿标记")
mode.add_argument("--final", action="store_true", help="最终素材模式，必须同时--reviewed")
p.add_argument("--reviewed", action="store_true", help="操作者已逐张查看本次输入图；此声明不等同真人游玩验证")
action = p.add_mutually_exclusive_group()
action.add_argument("--check-only", action="store_true", help="只校验素材与身份，普通Python即可，不写视频")
action.add_argument("--prepare-only", action="store_true", help="仅保存VSE工程与manifest")
action.add_argument("--preview-only", action="store_true", help="仅渲染排版检查PNG，不编码MP4")
p.add_argument("--width", type=int, choices=(1280, 1920), default=1920)
p.add_argument("--fps", type=int, choices=(12, 24, 30), default=24)
p.add_argument("--preview-sections", default="1,5,9", help="排版预览分段，1-based逗号分隔")
tokens = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
args = p.parse_args(tokens)
draft = not args.final
if args.final and not args.reviewed:
    p.error("最终导览必须在操作者查看最终素材后显式提供 --final --reviewed。")

def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def sha(path):
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()

shots = pathlib.Path(args.shots).resolve()
identity_path = pathlib.Path(args.identity).resolve()
run_path = pathlib.Path(args.run_config).resolve() if args.run_config else shots.parent / "run-config.json"
visual_path = pathlib.Path(args.visual_report).resolve() if args.visual_report else shots / "visual-v06-report.json"
font_path = pathlib.Path(args.font).resolve()
license_path = pathlib.Path(args.font_license).resolve()
story_path = pathlib.Path(args.storyboard).resolve()
out = pathlib.Path(args.output).resolve()
for path in (identity_path, run_path, visual_path, font_path, license_path, story_path):
    if not path.is_file():
        raise FileNotFoundError(str(path))
if out.suffix.lower() != ".mp4":
    raise ValueError("Output must be a new .mp4 file.")
identity, run, visual, story = map(read_json, (identity_path, run_path, visual_path, story_path))
if identity.get("status") != "Succeeded" or not identity.get("sourceUnchanged"):
    raise ValueError("构建身份不是源输入保持一致的成功构建。")
for key in ("assemblySha256", "sourceSha256", "version"):
    if not identity.get(key) or identity[key] != run.get(key):
        raise ValueError("截图运行记录与构建身份不一致：" + key)
if not visual.get("passed") or visual.get("version") != identity["version"]:
    raise ValueError("VisualV06QA未通过，或版本与该构建不一致。")
frames = visual.get("frames")
if not isinstance(frames, list) or not frames:
    raise ValueError("原生截图报告没有frames列表。")
by_id = {frame["id"]: frame for frame in frames}
if len(by_id) != len(frames):
    raise ValueError("原生截图报告存在重复frame id。")
sections = story.get("sections", [])
if not sections:
    raise ValueError("分镜不能为空。")
seconds = sum(float(section["seconds"]) for section in sections)
if abs(seconds - 45) > .01:
    raise ValueError("本导览须为45秒；当前为 " + str(seconds))
disclosure = "原生离屏截图导览，非实时游玩录像"
if story.get("disclosure") != disclosure:
    raise ValueError("必须保留明确的离屏静帧性质披露。")
screenshot_count = 0
frame_index = 1
for section in sections:
    duration = float(section["seconds"])
    if duration <= 0:
        raise ValueError("每段持续时间必须大于0。")
    section["start_frame"] = frame_index
    section["frame_count"] = round(duration * args.fps)
    section["end_frame"] = frame_index + section["frame_count"] - 1
    frame_index += section["frame_count"]
    if not section.get("frame_id"):
        if not section.get("steps"):
            raise ValueError("无图片段必须明确为带说明文字的尾卡。")
        section["evidence_type"] = "explanatory title card, not an in-game UI capture"
        continue
    screenshot_count += 1
    source = by_id.get(section["frame_id"])
    if source is None:
        raise ValueError("指定截图未列入本次原生报告：" + section["frame_id"])
    path = (shots / source["file"]).resolve()
    if not path.is_relative_to(shots) or not path.is_file() or path.suffix.lower() != ".png":
        raise ValueError("截图必须是指定目录内真实存在的PNG：" + str(path))
    with path.open("rb") as f:
        header = f.read(24)
    if header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("文件不是PNG：" + str(path))
    width, height = struct.unpack(">II", header[16:24])
    if [width, height] != [source.get("width"), source.get("height")]:
        raise ValueError("PNG尺寸与原生报告不一致：" + path.name)
    if source.get("includesScreenSpaceHud"):
        raise ValueError("本版文案对应离屏相机图，不应混入含HUD的其他采集方式。")
    section["file"] = str(path)
    section["file_sha256"] = sha(path)
    section["source_pixels"] = [width, height]
    section["native_capture"] = source
if screenshot_count > 8:
    raise ValueError("最多使用8张原生PNG。")
if screenshot_count == 0:
    raise ValueError("不能以纯文字卡冒充原生截图导览。")
preview_ids = [int(v) for v in args.preview_sections.split(",")]
if len(set(preview_ids)) != len(preview_ids) or any(v < 1 or v > len(sections) for v in preview_ids):
    raise ValueError("--preview-sections 必须指向唯一有效分段。")
brief = {"draft": draft, "source_version": identity["version"], "build": identity["assemblySha256"][:8],
         "screenshots": screenshot_count, "sections": len(sections), "seconds": seconds,
         "source_identity_matched": True, "source_images_reviewed_by_operator": args.reviewed}
if args.check_only:
    print("GUIDE_INPUT_CHECK " + json.dumps(brief, ensure_ascii=False), flush=True)
    raise SystemExit(0)

# Import only after preflight: ordinary-Python --check-only stays lightweight.
import bpy
bpy.context.preferences.filepaths.file_preview_type = "NONE"
out.parent.mkdir(parents=True, exist_ok=True)
manifest_path = out.with_suffix(".manifest.json")
blend_path = out.with_suffix(".blend")
license_copy = out.parent / (out.stem + "-font-license.txt")
previews = out.parent / (out.stem + "-frames")
for path in (out, manifest_path, blend_path, license_copy, previews):
    if path.exists():
        raise FileExistsError("保留已有产物，请选择新输出名：" + str(path))
previews.mkdir()
shutil.copyfile(license_path, license_copy)
W, H, fps = args.width, args.width * 9 // 16, args.fps
unit = W / 1920
total_frames = frame_index - 1
scene = bpy.context.scene
if scene.sequence_editor:
    scene.sequence_editor_clear()
editor = scene.sequence_editor_create()
scene.render.resolution_x = W
scene.render.resolution_y = H
scene.render.resolution_percentage = 100
scene.render.fps = fps
scene.frame_start = 1
scene.frame_end = total_frames
scene.render.use_sequencer = True
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.view_settings.exposure = 0
scene.view_settings.gamma = 1
font = bpy.data.fonts.load(str(font_path))
bg = editor.strips.new_effect(name="Neutral backdrop", type="COLOR", channel=1, frame_start=1, length=total_frames)
bg.color = (.006, .010, .014)

def text(name, value, start, length, channel, size, location, color=(.88, .92, .94, 1), anchor="LEFT"):
    strip = editor.strips.new_effect(name=name, type="TEXT", channel=channel, frame_start=start, length=length)
    strip.text = value
    strip.font = font
    strip.font_size = round(size * unit)
    strip.color = color
    strip.location = location
    strip.anchor_x = anchor
    strip.anchor_y = "CENTER"
    strip.alignment_x = anchor
    strip.blend_type = "ALPHA_OVER"
    strip.use_shadow = False
    return strip

header = story["title"] + "  v" + identity["version"] + "  /  " + identity["assemblySha256"][:8]
text("Guide identity", header, 1, total_frames, 5, 24, (.038, .963), (.43, .80, .86, 1))
text("Permanent disclosure", disclosure, 1, total_frames, 6, 24, (.962, .963), anchor="RIGHT")
text("Capture context", story["capture_note"], 1, total_frames, 7, 18, (.038, .021), (.60, .67, .70, 1))
if draft:
    text("Permanent draft label", "草稿 · 候选素材仅用于排版检查", 1, total_frames, 15, 21, (.962, .021), (1, .66, .31, 1), "RIGHT")

for index, entry in enumerate(sections):
    start, length = entry["start_frame"], entry["frame_count"]
    text("Source " + str(index + 1), entry["source_label"], start, length, 8, 19, (.038, .920), (.62, .71, .75, 1))
    if entry.get("frame_id"):
        image = bpy.data.images.load(entry["file"], check_existing=True)
        width, height = image.size[:]
        strip = editor.strips.new_image(name="Native PNG " + entry["frame_id"], filepath=entry["file"],
                                        channel=2, frame_start=start, fit_method="ORIGINAL")
        strip.frame_final_end = start + length
        scale = min((W - 112 * unit) / width, (H - 280 * unit) / height)
        strip.transform.scale_x = scale
        strip.transform.scale_y = scale
        strip.transform.offset_y = 32 * unit
        strip.blend_type = "ALPHA_OVER"
        strip.blend_alpha = 1
        entry["display_scale"] = scale
        entry["image_treatment"] = "uniform fit, no crop, no animation, no simulated HUD"
        text("Section title " + str(index + 1), str(index + 1).zfill(2) + "  " + entry["title"], start, length, 9, 36, (.038, .108))
        text("Section caption " + str(index + 1), entry["caption"], start, length, 10, 25, (.038, .061), (.70, .78, .82, 1))
    else:
        text("Entry title", entry["title"], start, length, 9, 52, (.5, .67), anchor="CENTER")
        text("Entry invitation", entry["caption"], start, length, 10, 29, (.5, .57), (.70, .82, .86, 1), "CENTER")
        for row, step in enumerate(entry["steps"]):
            text("Entry step " + str(row), step, start, length, 11 + row, 30, (.5, .45 - row * .09), anchor="CENTER")
        entry["image_treatment"] = "separate explanatory card; no gameplay pixels or UI invented"

def output_video():
    scene.render.image_settings.media_type = "VIDEO"
    scene.render.image_settings.file_format = "FFMPEG"
    scene.render.ffmpeg.format = "MPEG4"
    scene.render.ffmpeg.codec = "H264"
    scene.render.ffmpeg.constant_rate_factor = "MEDIUM"
    scene.render.ffmpeg.ffmpeg_preset = "GOOD"
    scene.render.ffmpeg.gopsize = fps * 2
    scene.render.ffmpeg.audio_codec = "NONE"
    scene.render.filepath = str(out)
    scene.render.use_file_extension = True

output_video()
# Pack image/font datablocks as references. VSE sources retain explicit local paths;
# the parameterized script, source PNGs and manifest are the rebuild contract.
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False, relative_remap=False)
manifest = dict(brief)
manifest.update({
    "kind": "Native offscreen still-frame explanatory guide, not real-time gameplay recording",
    "source_pixel_hashes_note": "PNG hashes recorded at guide render time; capture build linked by matched QA run-config and build identity.",
    "rendered": False, "render_validation_passed": False, "status": "prepared",
    "output": str(out), "timeline": str(blend_path), "resolution": [W, H], "fps": fps,
    "expected_frames": total_frames, "audio": None, "blender": bpy.app.version_string,
    "source_identity_file": str(identity_path), "source_identity_sha256": sha(identity_path),
    "build_identity": identity, "source_run_config": run,
    "visual_report_file": str(visual_path), "visual_report_sha256": sha(visual_path),
    "native_visual_report_passed": visual["passed"],
    "native_report_human_review_completed": visual.get("humanVisualReviewCompleted", False),
    "font": str(font_path), "font_sha256": sha(font_path),
    "font_license": str(license_copy), "font_license_sha256": sha(license_path),
    "storyboard_file": str(story_path), "storyboard_sha256": sha(story_path),
    "sections": sections, "previews": []
})
def save_manifest():
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
save_manifest()
if args.prepare_only:
    print("GUIDE_PREPARED " + str(blend_path), flush=True)
    raise SystemExit(0)

scene.render.image_settings.media_type = "IMAGE"
scene.render.image_settings.file_format = "PNG"
for index in preview_ids:
    section = sections[index - 1]
    frame = section["start_frame"] + section["frame_count"] // 2
    path = previews / ("section-" + str(index).zfill(2) + ".png")
    scene.frame_set(frame)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    manifest["previews"].append({"section": index, "frame": frame, "file": str(path)})
manifest["status"] = "layout_preview_only" if args.preview_only else "encoding"
save_manifest()
if args.preview_only:
    print("GUIDE_LAYOUT_PREVIEW " + json.dumps(manifest["previews"], ensure_ascii=False), flush=True)
    raise SystemExit(0)

output_video()
scene.frame_set(1)
bpy.ops.render.render(animation=True)
if not out.is_file() or out.stat().st_size < 10000:
    raise RuntimeError("Encoded MP4 is missing or empty.")
probe = bpy.data.scenes.new("Encoded guide validation")
bpy.context.window.scene = probe
probe.render.resolution_x = W
probe.render.resolution_y = H
probe.render.resolution_percentage = 100
probe.render.fps = fps
probe.render.use_sequencer = True
probe.view_settings.view_transform = "Standard"
probe.view_settings.look = "None"
movie = probe.sequence_editor_create().strips.new_movie(name="Actual encoded MP4", filepath=str(out), channel=1, frame_start=1, fit_method="FIT")
manifest["decoded_frames"] = movie.frame_duration
manifest["encoded_bytes"] = out.stat().st_size
manifest["has_avc1_tag"] = b"avc1" in out.read_bytes()
manifest["rendered"] = True
manifest["render_validation_passed"] = movie.frame_duration == total_frames and manifest["has_avc1_tag"]
probe.frame_end = total_frames
probe.frame_set(total_frames // 2)
probe.render.image_settings.media_type = "IMAGE"
probe.render.image_settings.file_format = "PNG"
probe.render.filepath = str(previews / "encoded-midpoint.png")
bpy.ops.render.render(scene=probe.name, write_still=True)
manifest["decoded_frame_png"] = probe.render.filepath
manifest["output_sha256"] = sha(out)
manifest["status"] = "render_complete" if manifest["render_validation_passed"] else "encoded_validation_failed"
save_manifest()
if not manifest["render_validation_passed"]:
    raise RuntimeError("MP4 duration/codec verification failed.")
print("GUIDE_RENDER_COMPLETE " + json.dumps({"output": str(out), "seconds": seconds, "frames": movie.frame_duration, "draft": draft}, ensure_ascii=False), flush=True)
