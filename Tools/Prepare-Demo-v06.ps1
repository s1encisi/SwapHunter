<#
.SYNOPSIS
显式重建项目资产引用、材质、配置与入口场景。
.DESCRIPTION
会写入 Game/Assets 和 ProjectSettings，先备份已有修改并查看差异。
普通 Build-Demo-v06.ps1 不会调用本命令。此命令不运行 Node.js/Blender，也不构建玩家。
.EXAMPLE
.\Tools\Prepare-Demo-v06.ps1 -EditorPath 'E:\Unity\6000.3.25f1\Editor\Unity.exe'
#>
[CmdletBinding()]
param([string]$EditorPath=$env:SWAPHUNTER_UNITY_EDITOR,[ValidateRange(60,7200)][int]$TimeoutSeconds=1800)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'BuildSupport-v06.ps1')
$root=Split-Path -Parent $PSScriptRoot
$editor=Get-SwapHunterEditor $EditorPath
$evidence=New-SwapHunterEvidenceDirectory (Join-Path $root ('Logs/V06/prepare-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')))
Write-SwapHunterJson (Join-Path $evidence 'source-before.json') (Get-SwapHunterSourceIdentity $root)
$log=Join-Path $evidence 'prepare.log'
$arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+(Join-Path $root 'Game')+'"'),
    '-executeMethod','SwapHunter.Editor.BuildDemo.Prepare','-prepareEvidence',('"'+$evidence+'"'),'-logFile',('"'+$log+'"'))
$process=Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$timedOut=-not $process.WaitForExit($TimeoutSeconds*1000)
if($timedOut){$process.Kill();$process.WaitForExit()}
Write-SwapHunterJson (Join-Path $evidence 'source-after.json') (Get-SwapHunterSourceIdentity $root)
if($timedOut -or $process.ExitCode -ne 0 -or -not(Test-Path -LiteralPath $log) -or
    -not(Select-String -LiteralPath $log -Pattern '^SWAPHUNTER_PREPARE=Succeeded' -Quiet)){
    throw "工程准备失败或超时，日志已保留：$log"
}
Write-Output "工程准备完成；请审阅源资产/配置差异后再构建。证据：$evidence"
