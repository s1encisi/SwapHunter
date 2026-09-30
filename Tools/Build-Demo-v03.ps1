# Compatibility entry point: current sources build v0.4; existing v0.3 artifacts remain intact.
param(
    [switch]$Development,
    [switch]$RunQA,
    [switch]$Benchmark,
    [string]$EditorPath = $env:SWAPHUNTER_UNITY_EDITOR,
    [switch]$DirectNetwork
)
& (Join-Path $PSScriptRoot 'Build-Demo-v04.ps1') @PSBoundParameters
