# UpdateVersion.ps1
# 用于在编译前自动更新项目版本号
# 
# 功能:
# 1. 读取项目当前版本号
# 2. 根据配置规则更新版本号
# 3. 将更新后的版本号写回相关文件

# 参数定义
param (
    [string]$VersionType = "build", # 默认更新构建号 (options: major, minor, build, revision)
    [string]$ProjectRoot = (Get-Item -Path "..\").FullName # 默认为脚本上级目录作为项目根目录
)

# 版本文件路径
$commonAssemblyInfoPath = Join-Path -Path $ProjectRoot -ChildPath "src\CommonAssemblyInfo.cs"

# 确保文件存在
if (-not (Test-Path $commonAssemblyInfoPath)) {
    Write-Error "无法找到版本文件: $commonAssemblyInfoPath"
    exit 1
}

# 读取当前版本
$versionPattern = '\[assembly: AssemblyVersion\("(\d+)\.(\d+)\.\*"\)\]'
$fileVersionPattern = '\[assembly: AssemblyFileVersion\("(\d+)\.(\d+)\.(\d+)\.(\d+)"\)\]'

$content = Get-Content -Path $commonAssemblyInfoPath -Raw
$versionMatch = [regex]::Match($content, $versionPattern)
$fileVersionMatch = [regex]::Match($content, $fileVersionPattern)

if (-not $versionMatch.Success -or -not $fileVersionMatch.Success) {
    Write-Error "无法在文件中找到版本信息"
    exit 1
}

# 解析当前版本
$majorVersion = [int]$fileVersionMatch.Groups[1].Value
$minorVersion = [int]$fileVersionMatch.Groups[2].Value
$buildVersion = [int]$fileVersionMatch.Groups[3].Value
$revisionVersion = [int]$fileVersionMatch.Groups[4].Value

# 更新版本号
switch ($VersionType) {
    "major" {
        $majorVersion++
        $minorVersion = 0
        $buildVersion = 0
        $revisionVersion = 0
    }
    "minor" {
        $minorVersion++
        $buildVersion = 0
        $revisionVersion = 0
    }
    "build" {
        $buildVersion++
        $revisionVersion = 0
    }
    "revision" {
        $revisionVersion++
    }
    default {
        Write-Warning "未知的版本类型: $VersionType, 默认更新构建号"
        $buildVersion++
    }
}

# 格式化新版本
$newAssemblyVersion = "$majorVersion.$minorVersion.*"
$newFileVersion = "$majorVersion.$minorVersion.$buildVersion.$revisionVersion"

# 更新文件内容
$updatedContent = $content -replace $versionPattern, "[assembly: AssemblyVersion(`"$newAssemblyVersion`")]"
$updatedContent = $updatedContent -replace $fileVersionPattern, "[assembly: AssemblyFileVersion(`"$newFileVersion`")]"

# 写回文件
Set-Content -Path $commonAssemblyInfoPath -Value $updatedContent

# 输出结果
Write-Host "版本已更新:"
Write-Host "AssemblyVersion: $newAssemblyVersion"
Write-Host "AssemblyFileVersion: $newFileVersion" 