# 导览与展示工具

这些可选工具处理 SwapHunter 的本地采集素材，不参与 Unity 构建或游戏运行。

## v0.6 中文机制导览

`make_mechanism_guide_v06.py` 配合 `storyboard-v06.json`，将同一次 VisualV06QA 的最多 8 张原生 PNG 制作为 **45 秒静帧导览**，另有一张独立体验说明尾卡。画面常驻“原生离屏截图导览，非实时游玩录像”，不补画游戏 HUD、不模拟动作，也不把编码帧率当作游戏帧率。

默认是带标记的草稿。只有最终 Visual 检查通过、对应构建身份一致，且操作者逐张查看最终图片后，才使用 `--final --reviewed`。图审声明不等同真人游玩验证。

依赖现有 Blender（本次验证为 5.2.2 LTS）；输入校验仅使用 Python 标准库。字体使用仓库已有的 Noto Sans SC，保留对应 `OFL.txt`。没有本机安装路径或素材目录的硬编码。

从仓库根目录运行以下 PowerShell 示例；先将验证目录替换为实际路径，并确保 `blender` 在 PATH 中：

```powershell
$visualRun = './Logs/V06/your-visual-run'
$guide = './Tools/Showcase/make_mechanism_guide_v06.py'
$guideArgs = @(
    '--shots', "$visualRun/visual-v06",
    '--identity', "$visualRun/build-identity.json",
    '--font', './Game/Assets/_SwapHunter/Fonts/NotoSansSC-Regular.otf',
    '--font-license', './Game/Assets/_SwapHunter/Fonts/OFL.txt',
    '--output', './work/mechanism-guide-draft.mp4'
)

# 只校验报告、图片与构建身份，不启动 Blender 或 Unity。
python $guide @guideArgs --draft --check-only

# Blender 后台生成草稿，不启动 Unity。
blender --background --factory-startup --python-exit-code 1 --python $guide -- @guideArgs --draft
```

- 只检查排版时，附加 `--preview-only --width 1280 --fps 12`；不会生成 MP4。
- `--run-config` 默认读取 PNG 目录上一层的 `run-config.json`，`--visual-report` 默认读取 PNG 目录内的 `visual-v06-report.json`；均可显式指定。
- 最终图审后，将输出改为新的文件名，例如 `./outputs/mechanism-guide.mp4`，再将末尾 `--draft` 换为 `--final --reviewed --width 1920 --fps 24`。
- 已有产物不会覆盖。产物包括 MP4、来源 manifest、排版预览、Blender 时间线和字体许可证副本。
- manifest 保留完整构建身份、原生采集设置、输入哈希及编码检查；片内只显示短构建标识。交付前还应查看实际 MP4 的中段、尾段及中文排版。
- 时间线保留本地素材路径；重建仍需原图、身份文件、脚本与分镜，不把单个 `.blend` 当作完整素材包。

## 历史工具

保留以下脚本，供既有素材与流程复用：

- `make_showcase.py`：历史 v0.4 截图展示。
- `make_trailer_v05.py`：从本地游戏素材建立 Blender 视频序列时间线。
- `mix_capture_audio.py`：根据游戏导出的声音事件重建展示片音轨。
- `verify_trailer.py`、`verify_video_project.py`：检查导出媒体和时间线素材的可移植性。

历史脚本的素材参数与 Blender、FFmpeg、Python 图像库需求以各自说明为准，不因使用新导览工具而增加这些依赖。截图轮播、脚本采集和概念图必须如实标注；动作采集也不自动构成帧率基准。素材随时间线保存时，应一并保留字体及其他许可证。
