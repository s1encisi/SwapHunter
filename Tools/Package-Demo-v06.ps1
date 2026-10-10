<#
.SYNOPSIS
生成同一已验证构建的本地 Windows ZIP、完整源码/作者 ZIP 与中文交付清单。
.DESCRIPTION
默认 Release：要求 Core/Missions/Storage/Gunplay/Visual 通过，Routes 必须完整覆盖十二关。
Candidate 会明确标为未完成验收。性能和真人状态单独列出，不因缺测而标记通过。
输入必须带 build-identity.json 和 source-manifest.json；当前构建输入不得与记录不同。
输出及独立解压目录必须全新、位于源码工程外。工具不启动 Unity/玩家/Blender，不提交或上传。
.PARAMETER ValidationDirectory
可传多个最终验证根目录，每个目录包含 summary、run-config、build-identity 和程序集哈希。
同套件的多个结果不得混选；原失败目录保留，但正式包明确选择最终同身份结果。
.PARAMETER VerificationRoot
独立解压的全新根目录。默认输出目录下 verification；这只是完整性核验，不是干净构建成功。
.PARAMETER CheckOnly
只检查身份、验收门槛与文件选择，不创建包或输出目录。
.EXAMPLE
.\Tools\Package-Demo-v06.ps1 -ExecutablePath 'Builds/SwapHunter-v0.6.0-final/SwapHunter.exe' -ValidationDirectory 'Logs/V06/final-logical','Logs/V06/final-routes','Logs/V06/final-visual' -OutputDirectory 'C:\Delivery\SwapHunter-v0.6.0'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ExecutablePath,
    [Parameter(Mandatory=$true)][string[]]$ValidationDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateSet('Release','Candidate')][string]$Mode='Release',
    [string]$VerificationRoot,[string]$PythonPath='python',[switch]$CheckOnly
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
function Resolve-InputPath([string]$Path){
    if([IO.Path]::IsPathRooted($Path)){return $Path}
    return Join-Path $root $Path
}
$arguments=@((Join-Path $PSScriptRoot 'package_demo_v06.py'),'--project',$root,
    '--executable',(Resolve-InputPath $ExecutablePath),'--output',(Resolve-InputPath $OutputDirectory),
    '--mode',$Mode.ToLowerInvariant())
foreach($directory in $ValidationDirectory){$arguments+=@('--validation-dir',(Resolve-InputPath $directory))}
if($VerificationRoot){$arguments+=@('--verification-root',(Resolve-InputPath $VerificationRoot))}
if($CheckOnly){$arguments+='--check-only'}
& $PythonPath @arguments
if($LASTEXITCODE -ne 0){throw "本地打包失败，未覆盖原件；请查看已保留的错误和新输出。退出码：$LASTEXITCODE"}
