<#
.SYNOPSIS
构建独立的 SwapHunter v0.7.0 Windows 玩家，并记录源码与程序集身份。
.DESCRIPTION
普通构建使用现有场景、材质和配置，不调用 Prepare。输出目录必须为空；旧构建不覆盖。
构建前后比较 Game/Assets、Packages、ProjectSettings；若 Unity 改变源输入则保留产物并报错。
.PARAMETER OutputDirectory
新的输出目录。默认 Builds/SwapHunter-v0.7.0；重复候选请另选路径。
.PARAMETER RunQA
构建后运行 Core、Missions、Routes、Storage 和 Gunplay 后台检查。
.EXAMPLE
.\Tools\Build-Demo-v06.ps1 -EditorPath 'E:\Unity\6000.3.25f1\Editor\Unity.exe' -RunQA
.EXAMPLE
.\Tools\Build-Demo-v06.ps1 -OutputDirectory 'Builds/SwapHunter-v0.7.0-candidate02'
#>
[CmdletBinding()]
param(
    [string]$EditorPath = $env:SWAPHUNTER_UNITY_EDITOR,
    [string]$OutputDirectory,
    [switch]$Development,
    [switch]$RunQA,
    [ValidateRange(60,7200)][int]$TimeoutSeconds = 1800
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'BuildSupport-v06.ps1')
$root = Split-Path -Parent $PSScriptRoot
$editor = Get-SwapHunterEditor $EditorPath
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $root 'Builds/SwapHunter-v0.7.0' }
elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $root $OutputDirectory }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -gt 0) {
    throw "已有构建不覆盖，请指定新的 -OutputDirectory：$OutputDirectory"
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$evidence = New-SwapHunterEvidenceDirectory (Join-Path $root ('Logs/V06/build-' + $stamp))
$before = Get-SwapHunterSourceIdentity $root
Write-SwapHunterJson (Join-Path $evidence 'source-before.json') $before
$executable = Join-Path $OutputDirectory 'SwapHunter.exe'
$log = Join-Path $evidence 'build.log'
$arguments = @('-batchmode','-force-d3d11','-projectPath',('"' + (Join-Path $root 'Game') + '"'),
    '-executeMethod','SwapHunter.Editor.BuildDemo.Build','-buildOutput',('"' + $executable + '"'),'-logFile',('"' + $log + '"'))
if ($Development) { $arguments += '-development' }
$started = [DateTime]::UtcNow
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
if ($timedOut) { $process.Kill(); $process.WaitForExit() }
$after = Get-SwapHunterSourceIdentity $root
Write-SwapHunterJson (Join-Path $evidence 'source-after.json') $after
$buildPassed = -not $timedOut -and $process.ExitCode -eq 0 -and (Test-Path -LiteralPath $log) -and
    (Select-String -LiteralPath $log -Pattern '^SWAPHUNTER_BUILD=Succeeded' -Quiet)
$identity = [ordered]@{
    schemaVersion=1;version='0.7.0';status='Failed';startedUtc=$started.ToString('o')
    completedUtc=[DateTime]::UtcNow.ToString('o');unityVersion='6000.3.25f1';target='StandaloneWindows64'
    development=[bool]$Development;sourceSha256=$before.sha256;sourceUnchanged=($before.sha256 -eq $after.sha256)
    gitCommit=$before.gitCommit;gitDirty=$before.gitDirty;gitChanges=$before.gitChanges
    exitCode=$process.ExitCode;timedOut=$timedOut;assemblySha256=$null;executableSha256=$null
}
if ($buildPassed -and $identity.sourceUnchanged) {
    $assembly = Join-Path $OutputDirectory 'SwapHunter_Data/Managed/Assembly-CSharp.dll'
    $identity.assemblySha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant()
    $identity.executableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
    $identity.status = 'Succeeded'
    Write-SwapHunterJson (Join-Path $OutputDirectory 'build-identity.json') $identity
    Write-SwapHunterJson (Join-Path $OutputDirectory 'source-manifest.json') $before
}
Write-SwapHunterJson (Join-Path $evidence 'build-identity.json') $identity
if (-not $identity.sourceUnchanged) { throw "构建期间源输入改变，产物不作为通过的交付。请审阅 source-before/after.json：$evidence" }
if ($identity.status -ne 'Succeeded') { throw "构建失败或超时，原始日志已保留：$log" }
Write-Output "已构建：$executable"
Write-Output "源输入 SHA256：$($identity.sourceSha256)；程序集 SHA256：$($identity.assemblySha256)"
if ($RunQA) { & (Join-Path $PSScriptRoot 'Validate-Demo-v06.ps1') -ExecutablePath $executable -OutputDirectory (Join-Path $evidence 'qa') }
