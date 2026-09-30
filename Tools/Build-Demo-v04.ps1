param(
    [switch]$Development,
    [switch]$RunQA,
    [switch]$Benchmark,
    [string]$EditorPath = $env:SWAPHUNTER_UNITY_EDITOR,
    [switch]$DirectNetwork
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$project=Join-Path $root 'Game'
if ([string]::IsNullOrWhiteSpace($EditorPath)) {
    $EditorPath = @(
        (Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'),
        'E:\Unity\6000.3.25f1\Editor\Unity.exe'
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
$editor=$EditorPath
$logs=Join-Path $root 'Logs\V04'
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
    $launchArgs=@('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod','SwapHunter.Editor.BuildDemo.Build','-logFile',('"'+$log+'"'))
    if($Development){$launchArgs+='-development'}
    $p=Start-Process -FilePath $editor -ArgumentList $launchArgs -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if($p.ExitCode -ne 0 -or -not(Select-String -LiteralPath $log -Pattern '^SWAPHUNTER_BUILD=Succeeded' -Quiet)){throw ('Build failed: '+$log)}
    $exe=Join-Path $root 'Builds\SwapHunter-v0.4\SwapHunter.exe'
    Write-Output ('Built: '+$exe)
    if($RunQA -or $Benchmark){
        $mode=if($Benchmark){'-swapHunterExpeditionBenchmark'}else{'-swapHunterQA'}
        $prefix=if($Benchmark){'benchmark-'}else{'qa-'}
        $out=Join-Path $logs ($prefix+$stamp);New-Item -ItemType Directory -Path $out -Force|Out-Null
        $launchArgs=@($mode,'-qaOutput',('"'+$out+'"'),'-logFile',('"'+(Join-Path $out 'player.log')+'"'),'-screen-fullscreen','0')
        if($Benchmark){$launchArgs+=@('-screen-width','1920','-screen-height','1080')}
        $p=Start-Process -FilePath $exe -ArgumentList $launchArgs -WindowStyle Normal -PassThru;$p.WaitForExit()
        if($p.ExitCode -ne 0){throw ('Validation failed: '+$out)}
        Write-Output ('Results: '+$out)
    }
}
finally{foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key],'Process')}}



