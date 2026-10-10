<#
.SYNOPSIS
执行无需 Unity 许可证的源文件、依赖、GUID/节点、配置和本地链接静态检查。
.DESCRIPTION
需要 Python 3.10+。不启动 Unity/Blender/游戏，不生成资产，不下载依赖。
检查不能代替编译、原生回归或真人体验。-ReportPath 只写新报告，不覆盖旧证据。
.EXAMPLE
.\Tools\Check-Project-v06.ps1 -PythonPath python -ReportPath 'Logs/V06/static-candidate01.json'
#>
[CmdletBinding()]
param([string]$PythonPath='python',[string]$ReportPath)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$arguments=@((Join-Path $PSScriptRoot 'check_project_v06.py'),'--root',$root)
if($ReportPath){$arguments+=@('--report',$ReportPath)}
& $PythonPath @arguments
if($LASTEXITCODE -ne 0){throw "静态工程检查失败，退出码：$LASTEXITCODE"}
& $PythonPath (Join-Path $PSScriptRoot 'test_check_project_v06.py')
if($LASTEXITCODE -ne 0){throw "检查器反例测试失败，退出码：$LASTEXITCODE"}
