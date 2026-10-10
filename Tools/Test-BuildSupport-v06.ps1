# Tests use inert text fixtures; no Unity/player process is launched.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'BuildSupport-v06.ps1')
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('swaphunter-build-support-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force|Out-Null
$checks=0
function Assert-Contract([bool]$Passed,[string]$Name){
    if(-not $Passed){throw "Build support contract failed: $Name"}
    $script:checks++
}
function Assert-Rejected([scriptblock]$Operation,[string]$Name){
    $rejected=$false
    try{& $Operation|Out-Null}catch{$rejected=$true}
    Assert-Contract $rejected $Name
}
try{
    foreach($name in @('Game/Assets','Game/Packages','Game/ProjectSettings','docs','player/SwapHunter_Data/Managed')){
        New-Item -ItemType Directory -Path (Join-Path $fixture $name) -Force|Out-Null
    }
    $code=Join-Path $fixture 'Game/Assets/Sample.cs'
    'original'|Set-Content -LiteralPath $code -Encoding UTF8
    $first=Get-SwapHunterSourceIdentity $fixture
    'documentation'|Set-Content -LiteralPath (Join-Path $fixture 'docs/note.md') -Encoding UTF8
    $second=Get-SwapHunterSourceIdentity $fixture
    Assert-Contract ($first.sha256 -eq $second.sha256) 'unrelated documents do not change build identity'
    Assert-Contract ($null -eq $second.gitCommit) 'source archive does not inherit an ancestor repository'
    'changed'|Set-Content -LiteralPath $code -Encoding UTF8
    $third=Get-SwapHunterSourceIdentity $fixture
    Assert-Contract ($third.sha256 -ne $second.sha256) 'changed build input changes identity'
    Write-SwapHunterJson (Join-Path $fixture 'one.json') @(@{name='single'})
    $json=Get-Content -LiteralPath (Join-Path $fixture 'one.json') -Raw -Encoding UTF8
    Assert-Contract ($json.TrimStart().StartsWith('[')) 'single result remains a JSON array'
    $exe=Join-Path $fixture 'player/SwapHunter.exe'
    $assembly=Join-Path $fixture 'player/SwapHunter_Data/Managed/Assembly-CSharp.dll'
    'inert executable fixture'|Set-Content -LiteralPath $exe -Encoding UTF8
    'inert assembly fixture'|Set-Content -LiteralPath $assembly -Encoding UTF8
    Assert-Rejected {Get-SwapHunterBuildIdentity $exe} 'unidentified player is rejected'
    $identity=@{
        version='0.6.0';status='Succeeded';sourceUnchanged=$true;sourceSha256=$third.sha256
        assemblySha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant()
        executableSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $identityFile=Join-Path $fixture 'player/build-identity.json'
    Write-SwapHunterJson $identityFile $identity
    $read=Get-SwapHunterBuildIdentity $exe
    Assert-Contract ($read.sourceSha256 -eq $third.sha256) 'matching source/player identity accepted'
    'different assembly'|Set-Content -LiteralPath $assembly -Encoding UTF8
    Assert-Rejected {Get-SwapHunterBuildIdentity $exe} 'replaced assembly rejected'
    $identity.sourceUnchanged=$false
    Write-SwapHunterJson $identityFile $identity
    Assert-Rejected {Get-SwapHunterBuildIdentity $exe} 'source mutation during build rejected'
    Write-Output "Build support contracts: $checks passed; no Unity/player started."
}finally{
    # Only the unique directory created above, beneath the system temp directory.
    $resolved=[IO.Path]::GetFullPath($fixture)
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
    if(-not $resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected fixture cleanup path'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
