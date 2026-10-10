# v0.6 共用函数；点源此文件不会启动程序或写入文件。
function Write-SwapHunterJson {
    param([string]$Path, $Value)
    ConvertTo-Json -InputObject $Value -Depth 12 | Set-Content -LiteralPath $Path -Encoding UTF8
}
function Get-SwapHunterSourceIdentity {
    param([string]$ProjectRoot)
    $ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd('\', '/')
    $scope = @('Game/Assets', 'Game/Packages', 'Game/ProjectSettings')
    if (Test-Path -LiteralPath (Join-Path $ProjectRoot 'Dependencies') -PathType Container) { $scope += 'Dependencies' }
    $files = @(
        foreach ($folder in $scope) {
            Get-ChildItem -LiteralPath (Join-Path $ProjectRoot $folder) -File -Recurse | ForEach-Object {
                [ordered]@{
                    path = $_.FullName.Substring($ProjectRoot.Length + 1).Replace('\', '/')
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
        }
    ) | Sort-Object { $_.path }
    $canonical = (@($files | ForEach-Object { $_.path + "`t" + $_.sha256 }) -join "`n") + "`n"
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try { $digest = ([BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-', '').ToLowerInvariant() }
    finally { $hasher.Dispose() }
    $commit = $null; $dirty = $null; $changes = @()
    if ((Test-Path -LiteralPath (Join-Path $ProjectRoot '.git')) -and (Get-Command git -ErrorAction SilentlyContinue)) {
        $head = & git -C $ProjectRoot rev-parse --verify HEAD 2>$null
        if ($LASTEXITCODE -eq 0) {
            $commit = "$head".Trim()
            $changes = @(& git --no-optional-locks -C $ProjectRoot status --porcelain=v1 --untracked-files=all -- @scope 2>$null)
            if ($LASTEXITCODE -ne 0) { throw '无法读取构建输入的 Git 状态。' }
            $dirty = $changes.Count -gt 0
        }
    }
    return [ordered]@{
        schemaVersion=1;scope=@($scope)
        sha256=$digest;gitCommit=$commit;gitDirty=$dirty;gitChanges=@($changes);files=@($files)
    }
}
function Get-SwapHunterEditor {
    param([string]$EditorPath)
    if ([string]::IsNullOrWhiteSpace($EditorPath)) {
        $EditorPath = @(
            (Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe'),
            'E:/Unity/6000.3.25f1/Editor/Unity.exe'
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace($EditorPath) -or -not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
        throw '请用 -EditorPath 或 SWAPHUNTER_UNITY_EDITOR 指定 Unity 6000.3.25f1 的 Unity.exe。'
    }
    return (Resolve-Path -LiteralPath $EditorPath).Path
}
function New-SwapHunterEvidenceDirectory {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) { throw "已有证据不覆盖，请选择新目录：$Path" }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    return (Resolve-Path -LiteralPath $Path).Path
}
function Get-SwapHunterBuildIdentity {
    param([string]$ExecutablePath)
    $directory = Split-Path -Parent $ExecutablePath
    $identityPath = Join-Path $directory 'build-identity.json'
    if (-not (Test-Path -LiteralPath $identityPath)) { throw '缺少 build-identity.json，请用 Build-Demo-v06.ps1 生成带身份的构建，并随包保留此文件。' }
    $identity = Get-Content -LiteralPath $identityPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($identity.version -ne '0.6.0' -or $identity.status -ne 'Succeeded' -or -not $identity.sourceUnchanged) {
        throw '构建身份不是通过源输入一致性检查的 v0.6.0 构建。'
    }
    $assembly = Join-Path $directory (([IO.Path]::GetFileNameWithoutExtension($ExecutablePath)) + '_Data/Managed/Assembly-CSharp.dll')
    $assemblyHash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant()
    $executableHash = (Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($assemblyHash -ne $identity.assemblySha256 -or $executableHash -ne $identity.executableSha256) {
        throw '当前可执行文件/程序集与构建身份不一致，请保留原包并重新构建或解压。'
    }
    return $identity
}
