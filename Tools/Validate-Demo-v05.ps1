param(
 [ValidateSet('Core','Routes','Performance','All')][string]$Suite='Core',
 [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$exe=Join-Path $projectRoot 'Builds\SwapHunter-v0.5\SwapHunter.exe'
if(-not(Test-Path -LiteralPath $exe)){throw 'Build v0.5 first with Tools/Build-Demo-v05.ps1.'}
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path $projectRoot ('Logs\V05\validation-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$definitions=@(
 @{suite='Core';name='legacy';flag='-swapHunterQA';report='qa-report.json'},
 @{suite='Core';name='expedition';flag='-swapHunterExpeditionQA';report='expedition-report.json'},
 @{suite='Core';name='campaign';flag='-swapHunterCampaignQA';report='campaign-report.json'},
 @{suite='Core';name='motion';flag='-swapHunterMotionQA';report='motion-report.json'},
 @{suite='Core';name='tactics';flag='-swapHunterTacticsQA';report='tactics-report.json'},
 @{suite='Core';name='boss';flag='-swapHunterBossSkillsQA';report='boss-skills-report.json'},
 @{suite='Core';name='boss-form';flag='-swapHunterBossQA';report='boss-report.json'},
 @{suite='Core';name='natural-boss';flag='-swapHunterNaturalBossQA';report='natural-boss-report.json'},
 @{suite='Core';name='tools';flag='-swapHunterCombatToolsQA';report='combat-tools-report.json'},
 @{suite='Core';name='ai';flag='-swapHunterTacticalAIQA';report='tactical-ai-report.json'},
 @{suite='Core';name='deployment';flag='-swapHunterDeploymentQA';report='deployment-report.json'},
 @{suite='Core';name='shop';flag='-swapHunterShopUIQA';report='shop-ui-report.json'},
 @{suite='Core';name='tutorial';flag='-swapHunterTutorialQA';report='tutorial-report.json'},
 @{suite='Core';name='battlefield';flag='-swapHunterBattlefieldQA';report='battlefield-report.json'},
 @{suite='Routes';name='routes';flag='-swapHunterRouteQA';report='route-report.json'},
 @{suite='Performance';name='boss-performance';flag='-swapHunterBossPerformanceQA';report='boss-performance.json'},
 @{suite='Performance';name='benchmark';flag='-swapHunterExpeditionBenchmark';report='expedition-benchmark.json'}
)
$cases=@($definitions | Where-Object {$Suite -eq 'All' -or $_.suite -eq $Suite})
$results=@()
foreach($case in $cases){
 $out=Join-Path $OutputDirectory $case.name;New-Item -ItemType Directory -Path $out -Force | Out-Null
 $argsList=@($case.flag,'-qaOutput',('"'+$out+'"'),'-logFile',('"'+(Join-Path $out 'player.log')+'"'),'-screen-fullscreen','0')
 if($case.name -in @('benchmark','boss-performance')){$argsList+=@('-screen-width','1920','-screen-height','1080')}
 $started=Get-Date;$process=Start-Process -FilePath $exe -ArgumentList $argsList -PassThru
 $process.WaitForExit();$path=Join-Path $out $case.report;$passed=$false;$checks=0
 if(Test-Path -LiteralPath $path){$r=Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json;$checks=$r.checks.Count;$passed=if($case.name -eq 'benchmark'){$r.measurementComplete -and $r.allRunsAverageAtLeast60 -and $r.allRunsP95AtMost16_67}else{$r.passed}}
 $entry=[pscustomobject]@{name=$case.name;passed=($passed -and $process.ExitCode -eq 0);exitCode=$process.ExitCode;checks=$checks;seconds=((Get-Date)-$started).TotalSeconds;report=$path}
 $results+=$entry;$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
 Write-Output ($entry.name+': passed='+$entry.passed+'; checks='+$checks)
}
$assembly=Join-Path $projectRoot 'Builds\SwapHunter-v0.5\SwapHunter_Data\Managed\Assembly-CSharp.dll'
Get-FileHash -LiteralPath $assembly -Algorithm SHA256 | Select-Object Algorithm,Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'assembly-sha256.json') -Encoding UTF8
if($results | Where-Object {-not $_.passed}){throw ('Validation failures: '+(Join-Path $OutputDirectory 'summary.json'))}
Write-Output ('Validation passed: '+$OutputDirectory)
