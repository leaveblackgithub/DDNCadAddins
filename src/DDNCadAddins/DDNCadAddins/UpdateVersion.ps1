# 版本号更新脚本
param (
    [string]$assemblyInfoPath = "Properties\AssemblyInfo.cs"
)

# 设置输出编码为UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 获取完整路径
$fullPath = Join-Path $PSScriptRoot $assemblyInfoPath
Write-Host "更新版本号：$fullPath"

# 读取文件内容
$content = Get-Content $fullPath -Raw

# 查找版本号
$versionPattern = '\[assembly: AssemblyVersion\("(\d+)\.(\d+)\.(\d+)\.(\d+)"\)\]'
$versionMatch = [regex]::Match($content, $versionPattern)

if ($versionMatch.Success) {
    # 提取当前版本号
    $major = [int]$versionMatch.Groups[1].Value
    $minor = [int]$versionMatch.Groups[2].Value
    $build = [int]$versionMatch.Groups[3].Value
    $revision = [int]$versionMatch.Groups[4].Value + 1
    
    # 创建新版本号
    $newVersion = "$major.$minor.$build.$revision"
    
    Write-Host "旧版本: $major.$minor.$build.$($revision-1)"
    Write-Host "新版本: $newVersion"
    
    # 更新AssemblyVersion
    $newAssemblyVersion = "[assembly: AssemblyVersion(""$newVersion"")]"
    $content = [regex]::Replace($content, $versionPattern, $newAssemblyVersion)
    
    # 更新AssemblyFileVersion
    $fileVersionPattern = '\[assembly: AssemblyFileVersion\("(\d+)\.(\d+)\.(\d+)\.(\d+)"\)\]'
    $newFileVersion = "[assembly: AssemblyFileVersion(""$newVersion"")]"
    $content = [regex]::Replace($content, $fileVersionPattern, $newFileVersion)
    
    # 写回文件
    Set-Content -Path $fullPath -Value $content
    
    Write-Host "版本号已更新为 $newVersion"
} else {
    Write-Host "未找到版本号信息"
} 