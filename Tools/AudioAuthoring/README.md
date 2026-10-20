# 枪械音频制作工具

`build_refined_audio.py` 从已保留署名的录音生成 12 把武器的差异化射击、装填与切枪音效，并生成 4 段项目原创命中提示。工具只处理音频资产，不修改射速、装填提交时点或伤害规则。

## 依赖与输入

已核验环境为 Python 3.11.14、NumPy 1.26.4、SciPy 1.16.0；后两项固定在 `requirements.txt`。工具使用 Python 标准库以及 NumPy、SciPy，不需要网络素材下载或专有音频软件。

| 输入路径（相对此目录） | 来源文件 | 作者与许可 |
|---|---|---|
| `audio_sources/shots/shots/{cg1,pistol,rifle,shotgun}.wav` | 四个同名原始录音 | Michel Baradari / apollo-music.de，[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/)，原包署名保存在同目录 `info.txt` |
| `audio_sources/gunreload.wav` | gunreload1.wav | SpringySpringo，CC0 |
| `audio_sources/riflereload.wav` | assaultriflereload1.wav | SpringySpringo，CC0 |
| `audio_sources/shotguncock.wav` | shotguncock.wav | SpringySpringo，CC0 |

原始发布页为 [枪响录音](https://opengameart.org/content/chaingun-pistol-rifle-shotgun-shots) 与 [装填录音](https://opengameart.org/content/gun-reload-sounds)。后者使用 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)；本地名称映射不改变录音来源。

## 生成

在仓库根目录运行：

```powershell
python -m venv work/audio-authoring-env
./work/audio-authoring-env/Scripts/python.exe -m pip install -r Tools/AudioAuthoring/requirements.txt
./work/audio-authoring-env/Scripts/python.exe Tools/AudioAuthoring/build_refined_audio.py
```

输出固定写入脚本旁的 `Tools/AudioAuthoring/audio_refined/`。重复运行会覆盖该生成目录内的同名输出，源录音保持不变，游戏资源目录不会自动更新。

生成内容为 88 个 48 kHz、16-bit、单声道 WAV，以及 `manifest.json` 和 `CREDITS.txt`：

- `gun_00..11_fire_a/b/c`：36 段射击变体。
- `gun_00..11_reload_out/reload_in/reload_bolt/equip`：48 段机械动作音效。
- `impact`、`limb_hit`、`head_hit`、`head_kill`：4 段程序合成命中提示。

生成清单记录每段音频的输入、采样数、时长、峰值、RMS 和 SHA-256。2026-10-20 使用以上依赖在独立目录重建后，88 个 WAV 全部与当前游戏音频逐字节一致；依赖版本变化后需重新核对输出。

## 部署与署名

先审核生成清单，再将输出复制到 `Game/Assets/_SwapHunter/Resources/Audio/Refinement/`。以下操作保留已有 Unity `.meta`，并在更新前备份同名文件；备份目录已存在时停止，避免覆盖先前备份。

```powershell
$audioGenerated = 'Tools/AudioAuthoring/audio_refined'
$audioTarget = 'Game/Assets/_SwapHunter/Resources/Audio/Refinement'
$audioBackup = Join-Path 'work/audio-backups' (Get-Date -Format 'yyyyMMdd-HHmmss')
if (Test-Path -LiteralPath $audioBackup) { throw 'Audio backup directory already exists' }
New-Item -ItemType Directory -Path $audioBackup | Out-Null
$audioFiles = Get-ChildItem -LiteralPath $audioGenerated -File | Where-Object { $_.Extension -eq '.wav' -or $_.Name -in @('manifest.json', 'CREDITS.txt') }
foreach ($audioFile in $audioFiles) {
    $audioDestination = Join-Path $audioTarget $audioFile.Name
    if (Test-Path -LiteralPath $audioDestination) { Copy-Item -LiteralPath $audioDestination -Destination $audioBackup }
    Copy-Item -LiteralPath $audioFile.FullName -Destination $audioDestination
}
```

随后让 Unity 重新导入音频，核对资源加载、波形峰值和音效事件；若产物变化，同步更新根 `THIRD_PARTY_ASSETS.json` 的产物哈希。88 段 WAV 的字节一致性不替代游戏运行检查和人工听感评审。

发布时将输出 `CREDITS.txt` 一并保留，可在玩家包根目录命名为 `THIRD_PARTY_AUDIO_OCTOBER.txt`。其中包含 Michel Baradari / apollo-music.de 的署名、CC BY 3.0 链接、原始来源及修改说明；不得将这些录音及其改编输出改标为项目 MIT 素材。完整资源台账见 [THIRD_PARTY_ASSETS.md](../../THIRD_PARTY_ASSETS.md)。
