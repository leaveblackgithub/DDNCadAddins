# DDNCadAddins 项目编译和版本更新脚本
# 版本 1.0.0

# 设置UTF-8编码
$OutputEncoding = [System.Text.Encoding]::UTF8

# 项目路径设置
$solutionPath = "D:\leaveblackgithub\DDNCadAddins\src\DDNCadAddins\DDNCadAddins.sln"
$projectPath = "D:\leaveblackgithub\DDNCadAddins\src\DDNCadAddins\DDNCadAddins\DDNCadAddins.csproj"
$vsPath = "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe"

# 创建备份
function Create-Backup {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupDir = "D:\leaveblackgithub\DDNCadAddins\code_backup_$timestamp"
    
    Write-Host "创建项目备份到: $backupDir" -ForegroundColor Cyan
    
    # 创建备份目录
    New-Item -ItemType Directory -Path $backupDir | Out-Null
    
    # 复制源代码文件
    $sourceDir = "D:\leaveblackgithub\DDNCadAddins\src"
    Copy-Item -Path $sourceDir -Destination $backupDir -Recurse -Force
    
    return $backupDir
}

# 编译项目
function Build-Project {
    Write-Host "正在编译项目..." -ForegroundColor Green
    
    # 使用MSBuild编译项目
    $msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    
    if (-not (Test-Path $msbuildPath)) {
        Write-Host "MSBuild路径不存在: $msbuildPath" -ForegroundColor Red
        Write-Host "尝试使用VS命令行编译..." -ForegroundColor Yellow
        
        $buildOutput = cmd /c "dotnet build `"$solutionPath`" /p:Configuration=Release /v:minimal 2>&1"
    } else {
        $buildOutput = & $msbuildPath $solutionPath /p:Configuration=Release /v:minimal
    }
    
    # 检查编译是否成功
    $buildSuccess = $LASTEXITCODE -eq 0
    
    if ($buildSuccess) {
        Write-Host "编译成功!" -ForegroundColor Green
    } else {
        Write-Host "编译失败!" -ForegroundColor Red
        Write-Host "错误信息:" -ForegroundColor Red
        Write-Host $buildOutput -ForegroundColor Gray
        
        # 提示在Visual Studio中打开解决方案
        Write-Host "请在Visual Studio中打开解决方案进行修复:" -ForegroundColor Yellow
        Write-Host "路径: $solutionPath" -ForegroundColor Yellow
        
        # 尝试自动打开Visual Studio
        if (Test-Path $vsPath) {
            $openVS = Read-Host "是否自动打开Visual Studio? (Y/N)"
            if ($openVS -eq "Y" -or $openVS -eq "y") {
                Write-Host "正在打开Visual Studio..." -ForegroundColor Cyan
                Start-Process $vsPath -ArgumentList "`"$solutionPath`""
            }
        }
    }
    
    return $buildSuccess
}

# 更新版本号
function Update-Version {
    param (
        [string]$versionType = "build" # 可选值: major, minor, build, revision
    )
    
    Write-Host "正在更新版本号..." -ForegroundColor Green
    
    # 读取Assembly信息文件
    $assemblyInfoPath = "D:\leaveblackgithub\DDNCadAddins\src\DDNCadAddins\DDNCadAddins\Properties\AssemblyInfo.cs"
    
    if (-not (Test-Path $assemblyInfoPath)) {
        Write-Host "Assembly信息文件不存在: $assemblyInfoPath" -ForegroundColor Red
        return $false
    }
    
    $content = Get-Content -Path $assemblyInfoPath -Raw
    
    # 查找当前版本号
    $versionPattern = '\[assembly: AssemblyVersion\("(\d+)\.(\d+)\.(\d+)\.(\d+)"\)\]'
    if ($content -match $versionPattern) {
        $major = [int]$Matches[1]
        $minor = [int]$Matches[2]
        $build = [int]$Matches[3]
        $revision = [int]$Matches[4]
        
        $oldVersion = "$major.$minor.$build.$revision"
        
        # 根据版本类型更新相应部分
        switch ($versionType) {
            "major" {
                $major++
                $minor = 0
                $build = 0
                $revision = 0
            }
            "minor" {
                $minor++
                $build = 0
                $revision = 0
            }
            "build" {
                $build++
                $revision = 0
            }
            "revision" {
                $revision++
            }
        }
        
        $newVersion = "$major.$minor.$build.$revision"
        
        # 更新版本号
        $content = $content -replace $versionPattern, "[assembly: AssemblyVersion(`"$newVersion`")]"
        $content = $content -replace '\[assembly: AssemblyFileVersion\("[\d\.]+"\)\]', "[assembly: AssemblyFileVersion(`"$newVersion`")]"
        
        # 保存文件
        $content | Set-Content -Path $assemblyInfoPath -Encoding UTF8
        
        Write-Host "版本号已更新: $oldVersion -> $newVersion" -ForegroundColor Green
        return $true
    } else {
        Write-Host "未找到版本号信息" -ForegroundColor Red
        return $false
    }
}

# 主程序
function Main {
    # 创建备份
    $backupDir = Create-Backup
    
    # 更新版本号
    Write-Host "请选择要更新的版本类型:" -ForegroundColor Cyan
    Write-Host "1. 主版本号 (major)" -ForegroundColor White
    Write-Host "2. 次版本号 (minor)" -ForegroundColor White
    Write-Host "3. 构建号 (build) - 默认" -ForegroundColor White
    Write-Host "4. 修订号 (revision)" -ForegroundColor White
    Write-Host "5. 不更新版本" -ForegroundColor White
    
    $choice = Read-Host "请输入选择 (1-5)"
    
    $versionUpdated = $false
    switch ($choice) {
        "1" { $versionUpdated = Update-Version -versionType "major" }
        "2" { $versionUpdated = Update-Version -versionType "minor" }
        "3" { $versionUpdated = Update-Version -versionType "build" }
        "4" { $versionUpdated = Update-Version -versionType "revision" }
        "5" { Write-Host "跳过版本更新" -ForegroundColor Yellow }
        default { $versionUpdated = Update-Version -versionType "build" }
    }
    
    # 编译项目
    $buildSuccess = Build-Project
    
    # 显示总结
    Write-Host "`n========== 执行摘要 ==========" -ForegroundColor Cyan
    Write-Host "项目备份: $backupDir" -ForegroundColor White
    Write-Host "版本更新: $(if ($versionUpdated) { "成功" } else { "失败或跳过" })" -ForegroundColor White
    Write-Host "项目编译: $(if ($buildSuccess) { "成功" } else { "失败" })" -ForegroundColor White
    Write-Host "===============================" -ForegroundColor Cyan
    
    if (-not $buildSuccess) {
        Write-Host "`n如需手动修复错误，请在Visual Studio中打开解决方案" -ForegroundColor Yellow
        Write-Host "解决方案路径: $solutionPath" -ForegroundColor Yellow
    }
}

# 执行主程序
Main 