<#
.SYNOPSIS
生成D1.1本地Windows试玩包和可公开源码包，不启动Unity/游戏、不上传。
.DESCRIPTION
默认同构建Tactical专项及真实5/7/12路线是门禁；-Profile models改为同构建Models专项+5/7/12路线，不宣称重跑经济。-Profile storyd2要求同构建StoryD2专项及新UI证据，不重跑旧套件。-Profile mapv2要求同构建完整MapV2专项及实际UI审阅，不强制旧D2/Synergy/Models/Routes。UI、性能、干净重建单独报告。
所选验证根包含build-identity、assembly-sha256、run-config和summary。
CC0与依赖许可按THIRD_PARTY_ASSETS.json逐项检查；旧打包器/旧包保留。
输出必须位于工程与原玩家目录之外的新目录，建议当前Codex工作区outputs下新建名称。
.EXAMPLE
.\Tools\Package-Tactical.ps1 -ExecutablePath 'Builds/Final/SwapHunter.exe' -ValidationDirectory 'Logs/D11/final-tactical','Logs/D11/final-routes' -OutputDirectory 'C:\Delivery\SwapHunter-D11-new'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ExecutablePath,
    [Parameter(Mandatory=$true)][string[]]$ValidationDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateSet('tactical','models','synergy','storyd2','mapv2')][string]$Profile='tactical',
    [string]$UIReview,
    [string]$SourceExecutablePath,
    [string]$VerificationRoot,
    [string]$PythonPath='python',
    [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
function Resolve-ProjectInput([string]$Value){
    if([IO.Path]::IsPathRooted($Value)){return $Value}
    return Join-Path $projectRoot $Value
}
$arguments=@('-B',(Join-Path $PSScriptRoot 'package_tactical.py'),
    '--project',$projectRoot,'--profile',($Profile.ToLowerInvariant()),'--executable',(Resolve-ProjectInput $ExecutablePath),
    '--output',(Resolve-ProjectInput $OutputDirectory))
foreach($directory in $ValidationDirectory){$arguments+=@('--validation-dir',(Resolve-ProjectInput $directory))}
if($UIReview){$arguments+=@('--ui-review',(Resolve-ProjectInput $UIReview))}
if($SourceExecutablePath){$arguments+=@('--source-executable',(Resolve-ProjectInput $SourceExecutablePath))}
if($VerificationRoot){$arguments+=@('--verification-root',(Resolve-ProjectInput $VerificationRoot))}
if($CheckOnly){$arguments+='--check-only'}
& $PythonPath @arguments
if($LASTEXITCODE -ne 0){throw "D1.1本地打包被拒绝或未完成；旧包和原文件保留，请查看错误及可能保留的新输出。退出码：$LASTEXITCODE"}