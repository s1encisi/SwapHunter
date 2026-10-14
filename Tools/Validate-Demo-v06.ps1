<#
.SYNOPSIS
验证同一 v0.6.0 构建，记录源码/程序集身份与原始报告。
.DESCRIPTION
默认后台执行 Core/Missions/Routes/Storage/Gunplay。Visual 为离屏截图，不提供实时性能结论。
Allocation 仅测同步托管分配，不代表帧率；Performance 仅显式 Foreground；Legacy 不能替代 v0.6 验收。All 不自动运行 Legacy。
不覆盖现有证据目录。每例有超时，失败报告原样保留。
.PARAMETER Suite
T01、Core、Missions、Routes、Storage、Gunplay、Visual、Allocation、Performance、BossEscape、Pool、Lifecycle、Legacy 或 All，可多选。
.PARAMETER ExecutablePath
新构建或独立解压包中的 SwapHunter.exe；同目录须保留 build-identity.json。
.PARAMETER DisplayMode
Background 不抢焦点；Foreground 只在用户明确安排前台时选择。
.EXAMPLE
.\Tools\Validate-Demo-v06.ps1 -Suite Core,Missions
.EXAMPLE
.\Tools\Validate-Demo-v06.ps1 -Suite Visual -OutputDirectory 'Logs/V06/visual-candidate01'
#>
[CmdletBinding()]
param(
    [ValidateSet('Synergy','Models','TacticalUI','Tactical','T01','Core','Missions','Routes','Storage','Gunplay','Visual','Allocation','Performance','BossEscape','Pool','Lifecycle','Legacy','All')]
    [string[]]$Suite=@('Core','Missions','Routes','Storage','Gunplay'),
    [string]$OutputDirectory,[string]$ExecutablePath,
    [ValidateSet('Background','Foreground')][string]$DisplayMode='Background',
    [string[]]$Cases,[ValidateRange(10,7200)][int]$CaseTimeoutSeconds=900,
    [ValidateRange(1,12)][int[]]$Levels,
    [ValidateSet('Baseline','After')][string]$PerformanceStage='Baseline',
    [switch]$AllocationDiagnostic,
    [ValidateSet('Sprint','Walk')][string]$BossEscapeMovement='Sprint',
    [ValidateRange(0,1.5)][double]$BossEscapeReactionSeconds=0.5
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'BuildSupport-v06.ps1')
$root=Split-Path -Parent $PSScriptRoot
if([string]::IsNullOrWhiteSpace($ExecutablePath)){$ExecutablePath=Join-Path $root 'Builds/SwapHunter-v0.6.0/SwapHunter.exe'}
elseif(-not[IO.Path]::IsPathRooted($ExecutablePath)){$ExecutablePath=Join-Path $root $ExecutablePath}
if(-not(Test-Path -LiteralPath $ExecutablePath -PathType Leaf)){throw '请先构建 v0.6.0，或指定独立解压包的 -ExecutablePath。'}
$ExecutablePath=(Resolve-Path -LiteralPath $ExecutablePath).Path
$identity=Get-SwapHunterBuildIdentity $ExecutablePath
$definitions=@(
    @{suite='Synergy';name='synergy';flag='-swapHunterSynergyQA';report='synergy-report.json'},
    @{suite='Models';name='models-final';flag='-swapHunterModelsFinalQA';report='models-final-report.json'},
    @{suite='TacticalUI';name='tactical-ui';flag='-swapHunterTacticalUIQA';report='tactical-ui-report.json'},
    @{suite='Tactical';name='tactical';flag='-swapHunterFullQA';report='tactical-campaign-report.json'},
    @{suite='T01';name='t01';flag='-swapHunterT01QA';report='t01-report.json'},
    @{suite='Core';name='core-v06';flag='-swapHunterV06CoreQA';report='core-v06-report.json'},
    @{suite='Missions';name='mission-v06';flag='-swapHunterV06MissionQA';report='mission-v06-report.json'},
    @{suite='Routes';name='route-v06';flag='-swapHunterV06RouteQA';report='route-v06-report.json'},
    @{suite='Storage';name='storage-v06';flag='-swapHunterV06StorageQA';report='storage-v06-report.json'},
    @{suite='Gunplay';name='gunplay';flag='-swapHunterGunplayQA';report='gunplay-presentation-report.json'},
    @{suite='Visual';name='visual-v06';flag='-swapHunterV06VisualQA';report='visual-v06-report.json'},
    @{suite='Allocation';name='allocation-v06';flag='-swapHunterV06AllocationQA';report='allocation-v06-report.json'},
    @{suite='BossEscape';name='boss-escape-v06';flag='-swapHunterV06BossEscapeQA';report='boss-escape-v06-report.json'},
    @{suite='Pool';name='pool-v06';flag='-swapHunterV06PoolQA';report='pool-v06-report.json'},
    @{suite='Lifecycle';name='lifecycle-v06';flag='-swapHunterV06LifecycleQA';report='lifecycle-v06-report.json'},
    @{suite='Performance';name='performance-v06';flag='-swapHunterV06PerformanceQA';report='performance-v06-report.json'},
    @{suite='Legacy';name='legacy-review-core';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','core')},
    @{suite='Legacy';name='legacy-review-systems';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','systems')},
    @{suite='Legacy';name='legacy-review-levels';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','levels')},
    @{suite='Legacy';name='legacy-flow';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','flow')}
)
if($Cases|Where-Object{$_ -notin $definitions.name}){throw 'Cases 包含未知案例名。'}
$selected=@($definitions|Where-Object{
    ($_.suite -in $Suite -or ('All' -in $Suite -and $_.suite -ne 'Legacy')) -and (-not $Cases -or $_.name -in $Cases)
})
if($selected.Count -eq 0){throw 'Suite 与 Cases 没有匹配的案例。'}
$deferred=@()
if($DisplayMode -eq 'Background'){
    $deferred=@($selected|Where-Object{$_.suite -in @('Performance','TacticalUI')}|ForEach-Object{$_.name})
    $selected=@($selected|Where-Object{$_.suite -notin @('Performance','TacticalUI')})
}
if($selected.Count -eq 0){throw '性能检查需要明确选择 Foreground；没有启动玩家。'}
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path $root ('Logs/V06/validation-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))}
elseif(-not[IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory=Join-Path $root $OutputDirectory}
$OutputDirectory=New-SwapHunterEvidenceDirectory $OutputDirectory
Write-SwapHunterJson (Join-Path $OutputDirectory 'build-identity.json') $identity
Write-SwapHunterJson (Join-Path $OutputDirectory 'assembly-sha256.json') @{Algorithm='SHA256';Hash=$identity.assemblySha256}
Write-SwapHunterJson (Join-Path $OutputDirectory 'run-config.json') @{
    version='0.6.0';suite=$Suite;displayMode=$DisplayMode;batchMode=($DisplayMode -eq 'Background');deferred=$deferred
    sourceSha256=$identity.sourceSha256;assemblySha256=$identity.assemblySha256;levels=$Levels;performanceStage=$PerformanceStage
    bossEscapeMovement=$BossEscapeMovement;bossEscapeReactionSeconds=$BossEscapeReactionSeconds
    evidence='Scripted native-player checks. Offscreen capture is not performance evidence or human playtesting.'
}
$results=@()
foreach($case in $selected){
    $current=Get-SwapHunterBuildIdentity $ExecutablePath
    if($current.assemblySha256 -ne $identity.assemblySha256 -or $current.sourceSha256 -ne $identity.sourceSha256){throw '验证期间构建身份发生变化，已保存的证据不再继续合并。'}
    $out=New-SwapHunterEvidenceDirectory (Join-Path $OutputDirectory $case.name)
    $arguments=@($case.flag,'-qaOutput',('"'+$out+'"'),'-logFile',('"'+(Join-Path $out 'player.log')+'"'),
        '-screen-fullscreen','0','-screen-width','1920','-screen-height','1080')
    if($case.extra){$arguments+=$case.extra}
    if($case.suite -eq 'Routes' -and $Levels){$arguments+=@('-v06Levels',($Levels -join ','))}
    if($case.suite -eq 'BossEscape'){$arguments+=@('-bossEscapeMovement',$BossEscapeMovement,'-bossEscapeReactionSeconds',$BossEscapeReactionSeconds.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture))}
    if($case.suite -in @('Performance','Allocation')){
        $arguments+=@('-performanceStage',$PerformanceStage,'-performanceDisplayMode',$DisplayMode)
        if($AllocationDiagnostic){$arguments+='-performanceAllocationDiagnostic'}
    }
    if($DisplayMode -eq 'Background'){$arguments+='-batchmode'}
    $started=Get-Date
    $windowStyle=if($DisplayMode -eq 'Background'){'Hidden'}else{'Normal'}
    $process=Start-Process -FilePath $ExecutablePath -WorkingDirectory (Split-Path -Parent $ExecutablePath) -ArgumentList $arguments -WindowStyle $windowStyle -PassThru
    $timedOut=-not $process.WaitForExit($CaseTimeoutSeconds*1000)
    if($timedOut){$process.Kill();$process.WaitForExit()}
    $reportPath=Join-Path $out $case.report
    $passed=$false;$checks=0;$frameSamples=0;$reportError=$null;$measurementKind=$null;$byteMeasurement=$null;$eventMeasurement=$null
    if(Test-Path -LiteralPath $reportPath){
        try{
            $report=Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8|ConvertFrom-Json
            $checks=if($null -eq $report.checks){0}else{@($report.checks).Count}
            if($case.suite -eq 'BossEscape'){
                $checks=@($report.attempts).Count
                $passed=$report.passed -eq $true -and $report.complete -eq $true -and $checks -eq ($report.expectedEscapeAttempts+1) -and
                    @($report.attempts|Where-Object{-not $_.passed}).Count -eq 0 -and
                    (Test-Path -LiteralPath (Join-Path $out 'boss-escape-v06-trajectory.csv'))
            }elseif($case.suite -eq 'Allocation'){
                $passed=$report.passed -eq $true -and $report.measurementValid -eq $true -and
                    $report.objectLifecycleMeasurementValid -eq $true -and
                    $report.completedBatches -eq $report.measuredBatches -and $report.completedBatches -gt 0 -and
                    $report.retiredDamageProbePassed -eq $true -and
                    $report.totalProductionRetired -eq $report.totalSuccessfulSpawnCalls
                $measurementKind=$report.outcome
                $byteMeasurement=$report.byteMeasurementComplete
                $eventMeasurement=$report.eventMeasurementComplete
            }elseif($case.suite -eq 'Performance'){
                $frameSamples=$report.frames
                $passed=$report.passed -eq $true -and $report.measurementValid -eq $true -and
                    $frameSamples -gt 0 -and $report.focusFraction -eq 1 -and
                    (Test-Path -LiteralPath (Join-Path $out 'performance-v06-frames.csv'))
            }else{$passed=$report.passed -eq $true -and $checks -gt 0}
        }catch{$reportError=$_.Exception.Message}
    }else{$reportError='Expected report is missing.'}
    $results+=[ordered]@{
        name=$case.name;suite=$case.suite;passed=($passed -and $process.ExitCode -eq 0 -and -not $timedOut)
        exitCode=$process.ExitCode;checks=$checks;frameSamples=$frameSamples;timedOut=$timedOut;seconds=((Get-Date)-$started).TotalSeconds
        report=($case.name+'/'+$case.report);reportError=$reportError;displayMode=$DisplayMode
        measurementKind=$measurementKind;byteMeasurementComplete=$byteMeasurement;eventMeasurementComplete=$eventMeasurement
        sourceSha256=$identity.sourceSha256;assemblySha256=$identity.assemblySha256
    }
    Write-SwapHunterJson (Join-Path $OutputDirectory 'summary.json') @($results)
    Write-Output "$($case.name)：passed=$passed；checks=$checks；exit=$($process.ExitCode)；timeout=$timedOut"
}
if($deferred.Count -gt 0){Write-Output ('未执行前台性能案例：'+($deferred -join ', '))}
if($results|Where-Object{-not $_.passed}){throw "存在失败/超时，原始证据已保留：$OutputDirectory"}
Write-Output "验证完成：$OutputDirectory"
