<#
.SYNOPSIS
手动打开 SwapHunter v0.6 独立演示档，默认展示第 2 关。
.DESCRIPTION
每个进程使用新的短临时目录，不加载或保存正式研究、武器、专精、检查点及 SH 设置。
全武器与飞刀3级为明确的演示种子；没有预置通关记录或分数，输入、伤害与任务规则正常。
此脚本是交互式启动入口；编写或查看脚本不会启动游戏。-WhatIf 仅显示待执行操作。
命令行参数只选择应用模式，不表达对自动化前台操作的授权。
.PARAMETER Level
展示章节，范围 1..12。建议 2（盾线）、6（手雷/近战破坏）、7（搬锚）、12（Boss）。
.PARAMETER ExecutablePath
本次包含独立演示功能的 Windows 玩家路径。不能使用旧版本代替。
.EXAMPLE
.\Tools\Start-Demo-v06.ps1 -Level 2
.EXAMPLE
.\Tools\Start-Demo-v06.ps1 -ExecutablePath 'D:\SwapHunter\SwapHunter.exe' -Level 7 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [ValidateRange(1,12)][int]$Level = 2,
    [string]$ExecutablePath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $projectRoot 'Builds/SwapHunter-v0.6.0/SwapHunter.exe'
} elseif (-not [IO.Path]::IsPathRooted($ExecutablePath)) {
    $ExecutablePath = Join-Path $projectRoot $ExecutablePath
}
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw '找不到新版玩家，请用 -ExecutablePath 指定已构建的 SwapHunter.exe。'
}
$ExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path
$directory = Split-Path -Parent $ExecutablePath
$assemblyPath = Join-Path $directory (([IO.Path]::GetFileNameWithoutExtension($ExecutablePath)) + '_Data/Managed/Assembly-CSharp.dll')
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw '缺少本项目的托管程序集，无法确认此包支持独立演示模式。'
}
# Old players ignore unknown switches and could open their ordinary profile.
# Read the compiled capability marker without loading/executing managed code.
$bytes = [IO.File]::ReadAllBytes($assemblyPath)
$marker = 'SWAPHUNTER_ISOLATED_DEMO_V1'
$aligned = [Text.Encoding]::Unicode.GetString($bytes)
$unaligned = if ($bytes.Length -gt 1) { [Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1) } else { '' }
if (-not $aligned.Contains($marker) -and -not $unaligned.Contains($marker)) {
    throw '此构建尚未包含独立演示入口；为避免旧程序忽略参数并打开正式档案，已拒绝启动。请使用本次新版构建。'
}
$log = Join-Path ([IO.Path]::GetTempPath()) ('SHD-' + [Guid]::NewGuid().ToString('N').Substring(0,12) + '.log')
$arguments = @('-swapHunterDemo', '-demoLevel', "$Level", '-logFile', ('"' + $log + '"'))
if ($PSCmdlet.ShouldProcess($ExecutablePath, "打开第 $Level 关独立演示窗口")) {
    # This launcher is invoked explicitly by a person for an interactive game.
    $process = Start-Process -FilePath $ExecutablePath -WorkingDirectory $directory -ArgumentList $arguments -WindowStyle Normal -PassThru
    Write-Host "已请求启动第 $Level 关演示档（PID $($process.Id)）；游戏窗口始终显示隔离标识。"
    Write-Host "Unity 日志：$log"
    Write-Host '实际临时档路径在日志 SWAPHUNTER_DEMONSTRATION_ROOT 一行；退出后保留供检查，不触碰正式进度。'
}
