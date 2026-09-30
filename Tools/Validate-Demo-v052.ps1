param(
 [ValidateSet('Core','Review','Gunplay','Flow','Routes','Performance','All')][string]$Suite='Review',
 [string]$OutputDirectory,
 [string]$ExecutablePath,
 [ValidateSet('Background','Foreground')][string]$DisplayMode='Background',
 [string[]]$Cases
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if([string]::IsNullOrWhiteSpace($ExecutablePath)){$ExecutablePath=Join-Path $projectRoot 'Builds\SwapHunter-v0.5.2\SwapHunter.exe'}
if(-not(Test-Path -LiteralPath $ExecutablePath)){throw 'Build v0.5.2 first, or pass -ExecutablePath for an independently extracted player.'}
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path $projectRoot ('Logs\V052\validation-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$definitions=@(
 @{suite='Gunplay';name='gunplay';flag='-swapHunterGunplayQA';report='gunplay-presentation-report.json'},
 @{suite='Core';name='legacy';flag='-swapHunterQA';report='qa-report.json'},
 @{suite='Core';name='campaign';flag='-swapHunterCampaignQA';report='campaign-report.json'},
 @{suite='Core';name='expedition';flag='-swapHunterExpeditionQA';report='expedition-report.json'},
 @{suite='Core';name='motion';flag='-swapHunterMotionQA';report='motion-report.json'},
 @{suite='Core';name='tactics';flag='-swapHunterTacticsQA';report='tactics-report.json'},
 @{suite='Core';name='boss-skills';flag='-swapHunterBossSkillsQA';report='boss-skills-report.json'},
 @{suite='Core';name='boss-form';flag='-swapHunterBossQA';report='boss-report.json'},
 @{suite='Core';name='natural-505';flag='-swapHunterNaturalBossQA';report='natural-boss-report.json';extra=@('-bossSeed','505')},
 @{suite='Core';name='natural-606';flag='-swapHunterNaturalBossQA';report='natural-boss-report.json';extra=@('-bossSeed','606')},
 @{suite='Core';name='natural-707';flag='-swapHunterNaturalBossQA';report='natural-boss-report.json';extra=@('-bossSeed','707')},
 @{suite='Core';name='combat-tools';flag='-swapHunterCombatToolsQA';report='combat-tools-report.json'},
 @{suite='Core';name='tactical-ai';flag='-swapHunterTacticalAIQA';report='tactical-ai-report.json'},
 @{suite='Core';name='deployment';flag='-swapHunterDeploymentQA';report='deployment-report.json'},
 @{suite='Core';name='shop';flag='-swapHunterShopUIQA';report='shop-ui-report.json'},
 @{suite='Core';name='tutorial';flag='-swapHunterTutorialQA';report='tutorial-report.json'},
 @{suite='Core';name='battlefield';flag='-swapHunterBattlefieldQA';report='battlefield-report.json'},
 @{suite='Review';name='review-core';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','core')},
 @{suite='Review';name='review-systems';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','systems')},
 @{suite='Review';name='review-levels';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','levels')},
 @{suite='Review';name='review-storage';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','storage','-swapHunterInteractiveReview')},
 @{suite='Flow';name='flow';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','flow')},
 @{suite='Flow';name='pursuit';flag='-swapHunterReviewFixQA';report='review-fixes-report.json';extra=@('-reviewGroup','levels','-reviewLevelPursuit')},
 @{suite='Routes';name='routes';flag='-swapHunterRouteQA';report='route-report.json'},
 @{suite='Performance';name='boss-performance';flag='-swapHunterBossPerformanceQA';report='boss-performance.json'},
 @{suite='Performance';name='benchmark';flag='-swapHunterExpeditionBenchmark';report='expedition-benchmark.json'}
)
$selected=@($definitions | Where-Object {($Suite -eq 'All' -or $_.suite -eq $Suite) -and (-not $Cases -or $_.name -in $Cases)})
if($Cases | Where-Object {$_ -notin $definitions.name}){throw 'Unknown case name.'}
if($selected.Count -eq 0){throw 'No cases match the requested suite and case names.'}
$deferred=@()
if($DisplayMode -eq 'Background'){
 $deferred=@($selected | Where-Object {$_.suite -notin @('Review','Gunplay','Flow')} | ForEach-Object {$_.name})
 $selected=@($selected | Where-Object {$_.suite -in @('Review','Gunplay','Flow')})
}
@{displayMode=$DisplayMode;batchMode=($DisplayMode -eq 'Background');deferred=$deferred;reason='Review and Flow support batch mode. Legacy rendered suites, routes and performance require a separately selected Foreground run; no automatic visible fallback.'} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'run-config.json') -Encoding UTF8
if($selected.Count -eq 0){throw 'These suites require an explicitly selected Foreground run; no game was started.'}
if($deferred.Count -gt 0){Write-Output ('Deferred rendered suites: '+($deferred -join ', '))}
$results=@()
foreach($case in $selected){
 $out=Join-Path $OutputDirectory $case.name
 if(Test-Path -LiteralPath $out){throw ('Existing evidence preserved; choose a new directory: '+$out)}
 New-Item -ItemType Directory -Path $out | Out-Null
 $argsList=@($case.flag,'-qaOutput',('"'+$out+'"'),'-logFile',('"'+(Join-Path $out 'player.log')+'"'),'-screen-fullscreen','0')
 if($case.extra){$argsList+=$case.extra}
 if($DisplayMode -eq 'Background'){$argsList+='-batchmode'}
 if($case.suite -in @('Review','Gunplay','Flow','Performance')){$argsList+=@('-screen-width','1920','-screen-height','1080')}
 $started=Get-Date
 $windowStyle=if($DisplayMode -eq 'Background'){'Hidden'}else{'Normal'}
 $process=Start-Process -FilePath $ExecutablePath -WorkingDirectory (Split-Path $ExecutablePath -Parent) -ArgumentList $argsList -WindowStyle $windowStyle -PassThru
 $process.WaitForExit();$path=Join-Path $out $case.report;$passed=$false;$checks=0
 if(Test-Path -LiteralPath $path){
  $r=Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json;$checks=$r.checks.Count
  $passed=if($case.name -eq 'benchmark'){$r.measurementComplete -and $r.allRunsAverageAtLeast60 -and $r.allRunsP95AtMost16_67 -and -not($r.runs | Where-Object {$_.focusFraction -lt .95})}else{$r.passed}
 }
 $results+=@{name=$case.name;passed=($passed -and $process.ExitCode -eq 0);exitCode=$process.ExitCode;checks=$checks;seconds=((Get-Date)-$started).TotalSeconds;report=$path;displayMode=$DisplayMode}
 ConvertTo-Json -InputObject @($results) -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
 Write-Output ($case.name+': passed='+$passed+'; checks='+$checks+'; exit='+$process.ExitCode)
}
$assembly=Join-Path (Split-Path $ExecutablePath -Parent) (([IO.Path]::GetFileNameWithoutExtension($ExecutablePath))+'_Data\Managed\Assembly-CSharp.dll')
Get-FileHash -LiteralPath $assembly -Algorithm SHA256 | Select-Object Algorithm,Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'assembly-sha256.json') -Encoding UTF8
if($results | Where-Object {-not $_.passed}){throw ('Validation failures preserved: '+(Join-Path $OutputDirectory 'summary.json'))}
