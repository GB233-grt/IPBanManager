[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$msbuild = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
$testProject = Join-Path $root "tests\CoreTests\CoreTests.csproj"

if (-not (Test-Path $msbuild)) {
    throw "未找到 MSBuild：$msbuild"
}

Write-Host "构建核心测试..." -ForegroundColor Cyan
& $msbuild $testProject /nologo /v:m /p:Configuration=$Configuration
if ($LASTEXITCODE -ne 0) { throw "测试项目构建失败。" }

$testExe = Join-Path $root "tests\CoreTests\bin\$Configuration\CoreTests.exe"
& $testExe
exit $LASTEXITCODE
