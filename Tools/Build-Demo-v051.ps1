param(
    [switch]$Development,
    [switch]$RunQA,
    [switch]$Benchmark,
    [string]$EditorPath = $env:SWAPHUNTER_UNITY_EDITOR,
    [switch]$DirectNetwork,
    [ValidateSet('Background','Foreground')][string]$DisplayMode='Background'
)
$ErrorActionPreference='Stop'
if($Benchmark -and $DisplayMode -ne 'Foreground'){throw 'Performance needs a separately requested foreground session. No editor or game was started.'}
$root=Split-Path -Parent $PSScriptRoot
$project=Join-Path $root 'Game'
if ([string]::IsNullOrWhiteSpace($EditorPath)) {
    $EditorPath = @(
        (Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'),
        'E:\Unity\6000.3.25f1\Editor\Unity.exe'
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
$editor=$EditorPath
$logs=Join-Path $root 'Logs\V051'
if([string]::IsNullOrWhiteSpace($editor) -or -not(Test-Path -LiteralPath $editor)){throw 'Pass -EditorPath <Unity.exe> or set SWAPHUNTER_UNITY_EDITOR to Unity 6000.3.25f1.'}
New-Item -ItemType Directory -Path $logs -Force|Out-Null
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$log=Join-Path $logs ('build-'+$stamp+'.log')
$saved=@{}
foreach($key in @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY','NO_PROXY')){$saved[$key]=[Environment]::GetEnvironmentVariable($key,'Process')}
try {
    if($DirectNetwork) {
        foreach($key in @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY')){[Environment]::SetEnvironmentVariable($key,$null,'Process')}
        $env:NO_PROXY='*'
    }
    $launchArgs=@('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod','SwapHunter.Editor.BuildDemo.Build','-buildOutput',('"'+(Join-Path $root 'Builds\SwapHunter-v0.5.1\SwapHunter.exe')+'"'),'-logFile',('"'+$log+'"'))
    if($Development){$launchArgs+='-development'}
    $p=Start-Process -FilePath $editor -ArgumentList $launchArgs -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if($p.ExitCode -ne 0 -or -not(Select-String -LiteralPath $log -Pattern '^SWAPHUNTER_BUILD=Succeeded' -Quiet)){throw ('Build failed: '+$log)}
    $exe=Join-Path $root 'Builds\SwapHunter-v0.5.1\SwapHunter.exe'
    Write-Output ('Built: '+$exe)
    if($RunQA -or $Benchmark){
        $suite=if($Benchmark){'Performance'}else{'Review'}
        $prefix=if($Benchmark){'benchmark-'}else{'qa-'}
        $out=Join-Path $logs ($prefix+$stamp);New-Item -ItemType Directory -Path $out -Force|Out-Null
        & (Join-Path $PSScriptRoot 'Validate-Demo-v051.ps1') -Suite $suite -ExecutablePath $exe -OutputDirectory $out -DisplayMode $DisplayMode
        Write-Output ('Results: '+$out)
    }
}
finally{foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key],'Process')}}



