# DDNCadAddins Project Code Fix Script
# For auto-fixing common syntax issues
# Version: 1.2.0

# Setting UTF-8 encoding
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
# Force UTF-8 without BOM for file output
$PSDefaultParameterValues['Out-File:Encoding'] = 'utf8NoBOM'
$PSDefaultParameterValues['*:Encoding'] = 'utf8'
# Ensure UTF-8 handling in PowerShell 5.1
$env:PYTHONIOENCODING = "utf-8"

# Create backup directory
$backupDir = "code_backup_" + (Get-Date).ToString("yyyyMMdd_HHmmss")
New-Item -ItemType Directory -Path $backupDir | Out-Null

Write-Host "Creating code backup to: $backupDir" -ForegroundColor Cyan

# Get all C# source files
$sourceFiles = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

Write-Host "Found $($sourceFiles.Count) C# source files" -ForegroundColor Cyan

# Create file backups
foreach ($file in $sourceFiles) {
    $relativePath = $file.FullName.Substring($PWD.Path.Length + 1)
    $backupPath = Join-Path -Path $backupDir -ChildPath $relativePath
    $backupFolder = Split-Path -Path $backupPath -Parent
    
    if (-not (Test-Path $backupFolder)) {
        New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
    }
    
    Copy-Item -Path $file.FullName -Destination $backupPath
}

Write-Host "Backup completed" -ForegroundColor Green

# Fix common issues
$fixedFileCount = 0
$changesCount = 0

foreach ($file in $sourceFiles) {
    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        
        # Skip if content is null or empty
        if ([string]::IsNullOrEmpty($content)) {
            Write-Host "Skipping empty file: $($file.FullName)" -ForegroundColor Yellow
            continue
        }
        
        $originalContent = $content
        $hasChanges = $false
        
        # 1. Fix: Add this prefix (SA1101)
        $regex = '(?<!this\.)\b([a-zA-Z_][a-zA-Z0-9_]*)\b(?=\s*[\.\(])'
        $propertyPattern = '\bpublic\s+([a-zA-Z_][a-zA-Z0-9_<>]*)\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*{\s*get;'
        
        # Get all property names
        $properties = @()
        if (![string]::IsNullOrEmpty($content)) {
            $propertyMatches = [regex]::Matches($content, $propertyPattern)
            $properties = $propertyMatches | ForEach-Object { $_.Groups[2].Value }
        }
        
        # Get all field names
        $fields = @()
        if (![string]::IsNullOrEmpty($content)) {
            $fieldPattern = '\bprivate\s+([a-zA-Z_][a-zA-Z0-9_<>]*)\s+_?([a-zA-Z_][a-zA-Z0-9_]*)\s*;'
            $fieldMatches = [regex]::Matches($content, $fieldPattern)
            $fields = $fieldMatches | ForEach-Object { 
                if ($_.Groups[2].Value.StartsWith("_")) {
                    $_.Groups[2].Value.Substring(1)
                } else {
                    $_.Groups[2].Value
                }
            }
        }
        
        # Combine property and field names
        $memberNames = $properties + $fields
        
        # Apply this prefix to each member name
        foreach ($memberName in $memberNames) {
            if ([string]::IsNullOrWhiteSpace($memberName)) { continue }
            
            $pattern = "(?<!\bthis\.)(?<!\w)($memberName)(?=\s*[\.\(])"
            $replacement = "this.$1"
            
            if (![string]::IsNullOrEmpty($content)) {
                $newContent = [regex]::Replace($content, $pattern, $replacement)
                if ($newContent -ne $content) {
                    $content = $newContent
                    $hasChanges = $true
                }
            }
        }
        
        # 2. Fix: Add braces to single-line statements (SA1503)
        $patterns = @(
            # if statement without braces
            '(\bif\s*\([^{;]*\))\s*([^{;\s][^{;]*;)'
            # else statement without braces
            '(\belse)\s*([^{;\s][^{;]*;)'
            # for loop without braces
            '(\bfor\s*\([^{;]*\))\s*([^{;\s][^{;]*;)'
            # foreach loop without braces
            '(\bforeach\s*\([^{;]*\))\s*([^{;\s][^{;]*;)'
            # while loop without braces
            '(\bwhile\s*\([^{;]*\))\s*([^{;\s][^{;]*;)'
        )
        
        if (![string]::IsNullOrEmpty($content)) {
            foreach ($pattern in $patterns) {
                $newContent = [regex]::Replace(
                    $content, 
                    $pattern, 
                    '$1 {$2}'
                )
                
                if ($newContent -ne $content) {
                    $content = $newContent
                    $hasChanges = $true
                }
            }
        }
        
        # 3. Fix: Add spaces around operators (SA1003)
        $operatorPatterns = @(
            # Fix + operator
            @('(\w)\+(\w)', '$1 + $2'),
            # Fix - operator
            @('(\w)-(\w)', '$1 - $2'),
            # Fix * operator
            @('(\w)\*(\w)', '$1 * $2'),
            # Fix / operator
            @('(\w)\/(\w)', '$1 / $2'),
            # Fix = operator
            @('(\w)=(\w)', '$1 = $2'),
            # Fix == operator
            @('(\w)==(\w)', '$1 == $2'),
            # Fix != operator
            @('(\w)!=(\w)', '$1 != $2'),
            # Fix && operator
            @('(\w)&&(\w)', '$1 && $2'),
            # Fix || operator
            @('(\w)\|\|(\w)', '$1 || $2')
        )
        
        if (![string]::IsNullOrEmpty($content)) {
            foreach ($pattern in $operatorPatterns) {
                $newContent = [regex]::Replace(
                    $content, 
                    $pattern[0], 
                    $pattern[1]
                )
                
                if ($newContent -ne $content) {
                    $content = $newContent
                    $hasChanges = $true
                }
            }
        }
        
        # 4. Fix: Use StringComparison (CA1310)
        $stringComparisonPatterns = @(
            # Fix StartsWith
            @('\.StartsWith\("(.+?)"\)', '.StartsWith("$1", StringComparison.Ordinal)'),
            # Fix EndsWith
            @('\.EndsWith\("(.+?)"\)', '.EndsWith("$1", StringComparison.Ordinal)'),
            # Fix IndexOf
            @('\.IndexOf\("(.+?)"\)', '.IndexOf("$1", StringComparison.Ordinal)'),
            # Fix single-parameter Contains
            @('\.Contains\("(.+?)"\)', '.Contains("$1", StringComparison.Ordinal)')
        )
        
        # If the file includes using System, add StringComparison
        if (![string]::IsNullOrEmpty($content) -and $content -match "using System;") {
            foreach ($pattern in $stringComparisonPatterns) {
                $newContent = [regex]::Replace(
                    $content, 
                    $pattern[0], 
                    $pattern[1]
                )
                
                if ($newContent -ne $content) {
                    $content = $newContent
                    $hasChanges = $true
                }
            }
        }
        
        # If there are changes, save the file
        if ($hasChanges) {
            $fixedFileCount++
            $changesCount++
            try {
                $utf8NoBom = New-Object System.Text.UTF8Encoding $false
                [System.IO.File]::WriteAllText($file.FullName, $content, $utf8NoBom)
                Write-Host "Fixed file: $($file.FullName)" -ForegroundColor Yellow
            }
            catch {
                Write-Host "Error writing to file: $($file.FullName) - $_" -ForegroundColor Red
            }
        }
    }
    catch {
        Write-Host "Error processing file: $($file.FullName) - $_" -ForegroundColor Red
    }
}

# Generate report
Write-Host "`nFix completed!" -ForegroundColor Green
Write-Host "Processed $($sourceFiles.Count) files, fixed $fixedFileCount files" -ForegroundColor Green
Write-Host "Code backup saved in directory: $backupDir" -ForegroundColor Green

Write-Host "`nNotes:" -ForegroundColor Yellow
Write-Host "1. This script can only fix some syntax issues, more complex problems need manual handling" -ForegroundColor Yellow
Write-Host "2. After fixing, please check the code using Visual Studio or other IDE" -ForegroundColor Yellow
Write-Host "3. If needed, restore files from the backup directory" -ForegroundColor Yellow

Write-Host "`nRecommended next steps:" -ForegroundColor Cyan
Write-Host "1. Run: cd src\DDNCadAddins && dotnet build" -ForegroundColor Cyan
Write-Host "2. Check compile errors and fix remaining issues manually" -ForegroundColor Cyan
Write-Host "3. Consider using Visual Studio's code cleanup for further improvements" -ForegroundColor Cyan 