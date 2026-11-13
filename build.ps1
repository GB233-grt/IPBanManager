[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [switch]$Preview,
    [switch]$SkipTests,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$msbuild = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
$appProject = Join-Path $root "src\IPBanManager\IPBanManager.csproj"
$testProject = Join-Path $root "tests\CoreTests\CoreTests.csproj"

function Write-Step([string]$text) {
    Write-Host ""
    Write-Host "==> $text" -ForegroundColor Cyan
}

function Invoke-MSBuild([string]$project, [string[]]$extraArgs) {
    if (-not (Test-Path $msbuild)) {
        throw "找不到 Windows 自带的 MSBuild：$msbuild"
    }
    $buildArgs = @($project, "/nologo", "/v:m", "/p:Configuration=$Configuration") + $extraArgs
    Write-Host "MSBuild $($buildArgs -join ' ')"
    & $msbuild @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild 失败，退出码 $LASTEXITCODE"
    }
}

if (-not (Test-Path $msbuild)) {
    throw "未找到 MSBuild。请确认系统为 Windows 10/11，并已启用 .NET Framework 4.8。"
}

$releaseKey = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -ErrorAction SilentlyContinue
if ($releaseKey -ne $null -and $releaseKey.Release -lt 528040) {
    throw "需要 .NET Framework 4.8（当前版本号 $($releaseKey.Release)）。Windows 10 1903 及以上系统自带。"
}

$running = Get-Process -Name "IPBanManager", "IPBanManager.Preview" -ErrorAction SilentlyContinue
if ($running) {
    throw "检测到净域正在运行，请先退出程序（包括托盘）再构建，否则 exe 文件会被占用。"
}

Write-Host "净域构建脚本" -ForegroundColor White
Write-Host "项目根目录：$root"
Write-Host "配置：$Configuration"
Write-Host "构建类型：$(if ($Preview) { '界面预览版（asInvoker）' } else { '正式版（requireAdministrator）' })"

if (-not $SkipTests) {
    Write-Step "构建并运行核心测试"
    Invoke-MSBuild $testProject @()
    $testExe = Join-Path $root "tests\CoreTests\bin\$Configuration\CoreTests.exe"
    if (-not (Test-Path $testExe)) { throw "测试程序未生成：$testExe" }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw "核心测试失败，已中止发布构建。" }
}

Write-Step "构建主程序"
if ($Preview) {
    Invoke-MSBuild $appProject @("/p:PreviewBuild=true")
    $sourceExe = Join-Path $root "src\IPBanManager\bin\Preview\IPBanManager.Preview.exe"
    $distDir = Join-Path $root "dist\净域-界面预览版"
    $zipPath = Join-Path $root "dist\净域-v1.0.0-界面预览版.zip"
    $exeName = "IPBanManager.Preview.exe"
} else {
    Invoke-MSBuild $appProject @()
    $sourceExe = Join-Path $root "src\IPBanManager\bin\$Configuration\IPBanManager.exe"
    $distDir = Join-Path $root "dist\净域"
    $zipPath = Join-Path $root "dist\净域-v1.0.0-win-x64.zip"
    $exeName = "IPBanManager.exe"
}

if (-not (Test-Path $sourceExe)) { throw "未找到构建输出：$sourceExe" }

Write-Step "整理发布目录"
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Copy-Item $sourceExe (Join-Path $distDir $exeName) -Force

$manual = Join-Path $root "docs\用户手册.md"
if (Test-Path $manual) { Copy-Item $manual (Join-Path $distDir "用户手册.md") -Force }

$license = Join-Path $root "LICENSE"
if (Test-Path $license) { Copy-Item $license (Join-Path $distDir "LICENSE") -Force }

$editionTitle = if ($Preview) { "净域 - 界面预览版 v1.0.0" } else { "净域 - 本地 HOSTS 封禁工具 v1.0.0" }
$quickSpec = if ($Preview) {
@"
  Windows 10 1903+ / Windows 11，x64，.NET Framework 4.8（系统自带）
  预览版以普通权限运行，不需要 UAC；HOSTS 指向临时目录，不会修改系统文件。
"@
} else {
@"
  Windows 10 1903+ / Windows 11，x64，.NET Framework 4.8（系统自带）
  首次启动会弹出 UAC 提权，这是写入系统 HOSTS 所必需的。
"@
}
$quickSteps = if ($Preview) {
@"
  1. 双击 $exeName，无需管理员权限即可打开界面。
  2. 可以浏览六个页面、编辑演示规则、预览主题。
  3. 所有改动只写入临时数据目录和临时 HOSTS，不修改系统 HOSTS。
  4. 正式使用时请运行发布包中的 IPBanManager.exe。
"@
} else {
@"
  1. 双击 $exeName，在 UAC 提示中选择“是”。
  2. 首页点击“添加封禁规则”，输入域名或网址，例如 bilibili.com。
  3. 可勾选“同时封禁常见子域”，一键加入 www / m / api 子域。
  4. 保存后会先备份 HOSTS，再写入托管区域并刷新 DNS 缓存。
  5. 删除、禁用或暂停分组后，规则会从 HOSTS 中移除或注释。
"@
}
$quickData = if ($Preview) {
@"
  临时数据目录：%TEMP%\IPBanManagerPreview
    data.json / settings.json / theme.json / preview-hosts / backups / logs
"@
} else {
@"
  数据目录：%APPDATA%\IPBanManager
    data.json       规则、分组、操作记录
    settings.json   高级设置
    theme.json      主题设置
    backups\        HOSTS 时间戳备份
    logs\           运行日志
"@
}
$quickStart = @"
$editionTitle
====================================

系统要求
$quickSpec

快速使用
$quickSteps

$quickData

安全边界
  只修改 HOSTS 文件和 DNS 缓存，不修改防火墙、不安装驱动、不修改网络适配器。
  HOSTS 屏蔽工作在域名解析层，无法按 IP 层阻断连接，也不能中断已建立的 HTTPS 连接。

卸载
  先在托盘菜单退出净域，然后在“高级设置”关闭开机自启，最后删除程序目录和
  数据目录即可。系统 HOSTS 中只保留程序自己的托管区域，卸载后可将该区域删除，
  块外内容不会被本程序改动。

更多说明见同目录“用户手册.md”。
"@
[System.IO.File]::WriteAllText((Join-Path $distDir "快速开始.txt"), $quickStart, (New-Object System.Text.UTF8Encoding($true)))

$hash = (Get-FileHash -Algorithm SHA256 (Join-Path $distDir $exeName)).Hash
$versionInfo = @"
产品：净域 - 本地 HOSTS 封禁工具
版本：1.0.0
构建时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
构建配置：$Configuration
构建类型：$(if ($Preview) { '界面预览版（HOSTS 指向临时目录，不写系统文件）' } else { '正式版（requireAdministrator）' })
目标框架：.NET Framework 4.8 / x64
许可证：MIT License
主程序：$exeName
SHA256：$hash
"@
[System.IO.File]::WriteAllText((Join-Path $distDir "版本信息.txt"), $versionInfo, (New-Object System.Text.UTF8Encoding($true)))

Write-Host ""
Write-Host "构建完成：" -ForegroundColor Green
Write-Host "  $distDir\$exeName"
Write-Host "  SHA256 $hash"

if (-not $NoZip) {
    Write-Step "生成发布压缩包"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    $zipParent = Split-Path -Parent $zipPath
    if (-not (Test-Path $zipParent)) { New-Item -ItemType Directory -Force -Path $zipParent | Out-Null }
    Compress-Archive -Path (Join-Path $distDir "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force
    Write-Host "  $zipPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "完成。正式版启动时将请求管理员权限。" -ForegroundColor White
