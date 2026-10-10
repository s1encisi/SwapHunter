<#
.SYNOPSIS
逐项回读 ZIP 并在全新目录独立解压，核对大小、SHA-256 和安全相对路径。
.DESCRIPTION
需要 Python 3.10+。目标父目录须存在，目标目录本身必须不存在。旧包、原文件及失败部分不会删除。
清单不含自身；可用 -Manifest 额外比对交付的独立清单。此操作不运行游戏，也不代表干净构建/QA完成。
.EXAMPLE
.\Tools\Verify-Package-v06.ps1 -Archive 'C:\Delivery\SwapHunter-v0.6.0-Source.zip' -Destination 'C:\Verify\source01' -Report 'C:\Verify\source01-report.json'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Archive,
    [Parameter(Mandatory=$true)][string]$Destination,
    [string]$Manifest,[string]$Report,[string]$PythonPath='python'
)
$ErrorActionPreference='Stop'
$arguments=@((Join-Path $PSScriptRoot 'verify_package_v06.py'),'--archive',$Archive,'--destination',$Destination)
if($Manifest){$arguments+=@('--manifest',$Manifest)}
if($Report){$arguments+=@('--report',$Report)}
& $PythonPath @arguments
if($LASTEXITCODE -ne 0){throw "归档完整性或独立解压核验失败；原包和已有文件保留。退出码：$LASTEXITCODE"}
