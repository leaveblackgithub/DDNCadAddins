# 清理未使用代码的PowerShell脚本
param(
    [string]$ReportFile = "unused_code_report.txt",
    [switch]$Interactive = $true,
    [switch]$DryRun = $true
)

function Get-UnusedCodeFromReport {
    param (
        [string]$ReportFile
    )
    
    if (-not (Test-Path $ReportFile)) {
        Write-Error "报告文件不存在: $ReportFile"
        return @()
    }
    
    $lines = Get-Content $ReportFile | Where-Object { $_ -match '\S' } | Select-Object -Skip 2
    $unusedItems = @()
    
    foreach ($line in $lines) {
        if ($line -match '(\S+)\s+(.+?)\s+(\d+)\s+(.+)') {
            $unusedItems += [PSCustomObject]@{
                Component = $matches[1]
                Message = $matches[2]
                Line = [int]$matches[3]
                Path = $matches[4]
            }
        }
    }
    
    return $unusedItems
}

function Remove-UnusedMember {
    param (
        [string]$FilePath,
        [int]$LineNumber,
        [string]$Message,
        [switch]$DryRun
    )
    
    if (-not (Test-Path $FilePath)) {
        Write-Warning "文件不存在: $FilePath"
        return $false
    }
    
    $content = Get-Content $FilePath
    
    # 试图识别整个成员（方法、属性等）的范围
    $startLine = $LineNumber - 1 # 0-based index
    $endLine = $startLine
    
    # 从开始行向下查找整个成员的结束位置（查找匹配的花括号）
    $braceCount = 0
    $inComment = $false
    $memberFound = $false
    
    for ($i = $startLine; $i -lt $content.Count; $i++) {
        $line = $content[$i]
        
        # 忽略注释
        if ($line -match '/\*') { $inComment = $true }
        if ($inComment) {
            if ($line -match '\*/') { $inComment = $false }
            continue
        }
        
        # 计算花括号
        $openBraces = ([regex]::Matches($line, '{').Count)
        $closeBraces = ([regex]::Matches($line, '}').Count)
        
        if ($i -eq $startLine) {
            $memberFound = $true
            $braceCount += $openBraces
        }
        else {
            $braceCount += $openBraces - $closeBraces
        }
        
        $endLine = $i
        
        # 如果找到匹配的花括号集，则结束
        if ($memberFound -and $braceCount -eq 0 -and $i -gt $startLine) {
            break
        }
    }
    
    # 显示将要删除的代码
    $codeToRemove = $content[$startLine..$endLine] -join "`n"
    Write-Host "`n准备删除未使用的代码:" -ForegroundColor Yellow
    Write-Host "文件: $FilePath" -ForegroundColor Cyan
    Write-Host "行号: $($startLine+1) 到 $($endLine+1)" -ForegroundColor Cyan
    Write-Host "代码:" -ForegroundColor Cyan
    Write-Host $codeToRemove -ForegroundColor Gray
    
    if ($DryRun) {
        Write-Host "模拟运行，不实际删除代码" -ForegroundColor Magenta
        return $true
    }
    
    # 实际删除代码
    $newContent = @()
    for ($i = 0; $i -lt $content.Count; $i++) {
        if ($i -lt $startLine -or $i -gt $endLine) {
            $newContent += $content[$i]
        }
    }
    
    $newContent | Set-Content $FilePath
    Write-Host "已从 $FilePath 删除未使用的代码" -ForegroundColor Green
    return $true
}

# 主程序
$unusedItems = Get-UnusedCodeFromReport -ReportFile $ReportFile

if ($unusedItems.Count -eq 0) {
    Write-Host "没有找到未使用的代码项" -ForegroundColor Green
    exit
}

Write-Host "找到 $($unusedItems.Count) 个未使用的代码项" -ForegroundColor Yellow

$itemsProcessed = 0
foreach ($item in $unusedItems) {
    $filePath = $item.Path
    if (-not [System.IO.Path]::IsPathRooted($filePath)) {
        $filePath = Join-Path (Get-Location) $filePath
    }
    
    Write-Host "`n[$($itemsProcessed+1)/$($unusedItems.Count)] $($item.Message)" -ForegroundColor Cyan
    
    $proceed = $true
    if ($Interactive) {
        $response = Read-Host "是否处理此项? (Y/N/All/Quit)"
        if ($response -eq "N") {
            $proceed = $false
        }
        elseif ($response -eq "All") {
            $Interactive = $false
        }
        elseif ($response -eq "Quit") {
            break
        }
    }
    
    if ($proceed) {
        $success = Remove-UnusedMember -FilePath $filePath -LineNumber $item.Line -Message $item.Message -DryRun:$DryRun
        if ($success) {
            $itemsProcessed++
        }
    }
}

Write-Host "`n处理完成。总共处理了 $itemsProcessed 个未使用的代码项。" -ForegroundColor Green
if ($DryRun) {
    Write-Host "这只是一次模拟运行，未实际修改文件。" -ForegroundColor Magenta
    Write-Host "若要实际修改文件，请使用: .\clean-unused-code.ps1 -DryRun:$false" -ForegroundColor Magenta
} 