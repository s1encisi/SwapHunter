param(
    [string]$EditorPath = $env:SWAPHUNTER_UNITY_EDITOR,
    [switch]$DirectNetwork,
    [int]$WaitForPreviewId = 0,
    [string]$ExpectedPreviewExe = ''
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if($WaitForPreviewId -gt 0){
    if([string]::IsNullOrWhiteSpace($ExpectedPreviewExe)){throw 'ExpectedPreviewExe is required when waiting for an interactive preview.'}
    $previewMeta=Get-CimInstance Win32_Process -Filter ('ProcessId='+$WaitForPreviewId)
    if($previewMeta){
        if($previewMeta.Name -ne 'SwapHunter.exe' -or $previewMeta.ExecutablePath -ne [IO.Path]::GetFullPath($ExpectedPreviewExe)){throw 'Preview process identity changed; nothing was stopped or launched.'}
        $previewProcess=Get-Process -Id $WaitForPreviewId
        Write-Output ('WAITING_FOR_PREVIEW_EXIT='+$WaitForPreviewId)
        $previewProcess.WaitForExit()
        Write-Output 'PREVIEW_EXIT_CONFIRMED'
    }
}
$interactive=@(Get-CimInstance Win32_Process -Filter "Name='SwapHunter.exe'" | Where-Object {$_.CommandLine -notmatch '-swapHunter(QA|CampaignQA|ExpeditionQA|RouteQA|ExpeditionBenchmark|Benchmark|AVReview|AudioReview)'})
if($interactive.Count -gt 0){throw 'An interactive SwapHunter instance is still running; validation did not take over or close it.'}
$renderers=@(Get-CimInstance Win32_Process -Filter "Name='blender.exe'")
if($renderers.Count -gt 0){throw 'Blender is running; leave it intact and run isolated graphics validation after it exits.'}
$batch=Join-Path $root ('Logs\V04\release-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $batch -Force|Out-Null
$summary=[ordered]@{version='0.4.0';startedUtc=(Get-Date).ToUniversalTime().ToString('o');completed=$false;passed=$false;performanceTargetsMet=$false;sourceRoot=$root;cases=@()}
$summaryFile=Join-Path $batch 'validation-batch.json'
function Save-Summary{$summary|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $summaryFile -Encoding utf8}
Save-Summary
& (Join-Path $PSScriptRoot 'Build-Demo-v04.ps1') -EditorPath $EditorPath -DirectNetwork:$DirectNetwork
$exe=Join-Path $root 'Builds\SwapHunter-v0.4\SwapHunter.exe'
$assembly=Join-Path $root 'Builds\SwapHunter-v0.4\SwapHunter_Data\Managed\Assembly-CSharp.dll'
$summary.assemblySha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant()
$summary.runtimeSources=@(Get-ChildItem -LiteralPath (Join-Path $root 'Game\Assets\_SwapHunter\Scripts') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object {@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
Save-Summary
$cases=@(
 @{name='performance';flag='-swapHunterExpeditionBenchmark';report='expedition-benchmark.json';width=1920;height=1080},
 @{name='expedition';flag='-swapHunterExpeditionQA';report='expedition-report.json';width=1280;height=720},
 @{name='ui-1080';flag='-swapHunterExpeditionQA';extra='-qaUiOnly';report='expedition-report.json';width=1920;height=1080},
 @{name='legacy';flag='-swapHunterQA';report='qa-report.json';width=1280;height=720},
 @{name='campaign';flag='-swapHunterCampaignQA';report='campaign-report.json';width=1280;height=720},
 @{name='routes';flag='-swapHunterRouteQA';report='route-report.json';width=1280;height=720},
 @{name='av';flag='-swapHunterAVReview';report='av-review.json';width=1280;height=720},
 @{name='audio';flag='-swapHunterAudioReview';report='audio-review.json';width=1280;height=720}
)
foreach($case in $cases){
    $out=Join-Path $batch $case.name;New-Item -ItemType Directory -Path $out -Force|Out-Null
    $launchArgs=@($case.flag)
    if($case.name -eq 'audio'){$launchArgs+=('"'+(Join-Path $out 'clips')+'"')}
    if($case.extra){$launchArgs+=$case.extra}
    $launchArgs+=@('-qaOutput',('"'+$out+'"'),'-logFile',('"'+(Join-Path $out 'player.log')+'"'),'-screen-fullscreen','0','-screen-width',([string]$case.width),'-screen-height',([string]$case.height))
    $style=if($case.name -in @('routes','audio')){'Hidden'}else{'Normal'}
    Write-Output ('START_VALIDATION='+$case.name)
    $started=(Get-Date).ToUniversalTime()
    $process=Start-Process -FilePath $exe -ArgumentList $launchArgs -WindowStyle $style -PassThru
    $process.WaitForExit()
    $files=@(Get-ChildItem -LiteralPath $out -Filter $case.report -Recurse -File)
    $entry=[ordered]@{name=$case.name;exitCode=$process.ExitCode;startedUtc=$started.ToString('o');finishedUtc=(Get-Date).ToUniversalTime().ToString('o');passed=$false;report=''}
    if($files.Count -eq 1){
        $entry.report=$files[0].FullName
        $report=Get-Content -LiteralPath $files[0].FullName -Raw|ConvertFrom-Json
        if($case.name -eq 'performance'){
            $entry.passed=$process.ExitCode -eq 0 -and $report.measurementComplete
            $summary.performanceTargetsMet=[bool]($report.allRunsAverageAtLeast60 -and $report.allRunsP95AtMost16_67)
        }elseif($case.name -eq 'audio'){
            $entry.passed=$process.ExitCode -eq 0 -and $report.clips.Count -gt 0 -and @($report.clips|Where-Object {-not $_.finite -or $_.peak -gt 1}).Count -eq 0
            $entry.clipCount=$report.clips.Count
        }else{
            $entry.passed=$process.ExitCode -eq 0 -and $report.passed
            if($report.checks){$entry.checkCount=$report.checks.Count}
        }
    }
    $summary.cases+=$entry;Save-Summary
    Write-Output ('VALIDATION_RESULT='+$case.name+';passed='+$entry.passed+';report='+$entry.report)
    if(-not $entry.passed){throw ('Validation failed, inspect '+$summaryFile)}
}
$summary.completed=$true
$summary.passed=@($summary.cases|Where-Object {-not $_.passed}).Count -eq 0 -and $summary.performanceTargetsMet
$summary.finishedUtc=(Get-Date).ToUniversalTime().ToString('o');Save-Summary
Write-Output ('FINAL_VALIDATION_REPORT='+$summaryFile)
if(-not $summary.performanceTargetsMet){Write-Warning 'Functional/evidence runs completed, but one or more performance thresholds were not met. This is not final acceptance.'}

