# DDNCadAddins Project - Syntax Analysis Script
# This script analyzes build errors and code quality issues
# Version: 1.2.0

# Setting UTF-8 encoding for output
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
# Force UTF-8 without BOM for file output
$PSDefaultParameterValues['Out-File:Encoding'] = 'utf8'
$PSDefaultParameterValues['*:Encoding'] = 'utf8'

Write-Host "DDNCadAddins Project - Syntax Analysis" -ForegroundColor Blue
Write-Host "----------------------------------------" -ForegroundColor Blue

# Check if reports directory exists
if (-not (Test-Path -Path "reports")) {
    Write-Host "Creating reports directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path "reports" | Out-Null
}

# Check if log file exists
$logFilePath = "reports\syntax_report.log"
if (-not (Test-Path -Path $logFilePath)) {
    Write-Host "Error: Log file not found at $logFilePath" -ForegroundColor Red
    Write-Host "Please run build.bat first to generate the log file." -ForegroundColor Red
    exit 1
}

Write-Host "Reading log file: $logFilePath" -ForegroundColor Green
$logContent = Get-Content -Path $logFilePath -Raw -Encoding UTF8

# Initialize error categories
$styleCopErrors = @{}
$codeAnalysisErrors = @{}
$compilerErrors = @{}
$otherErrors = @{}

# Regular expressions for identifying error types
$styleCopPattern = "SA\d{4}"
$codeAnalysisPattern = "(CA|CS)\d{4}"
$compilerPattern = "error CS\d{4}"
$errorLinePattern = ".*error.*"

# Error code to English description mappings
$compilerErrorDescriptions = @{
    # Compiler errors
    "CS1001" = "Identifier expected"
    "CS1002" = "Semicolon expected"
    "CS1003" = "Syntax error, ',' expected"
    "CS1026" = "Closing parenthesis expected"
    "CS1073" = "Unexpected token 'this'"
    "CS1513" = "Closing brace expected"
    "CS1525" = "Invalid expression term"
    "CS0246" = "Type could not be found"
    "CS0103" = "Name does not exist in current context"
    "CS0234" = "Type or namespace does not exist in namespace"
    "CS0168" = "Variable declared but never used"
    "CS0169" = "Field never used"
    "CS0219" = "Variable assigned but never used"
    "CS0649" = "Field never assigned to"
    "CS1591" = "Missing XML comment for publicly visible type or member"
    
    # Style errors
    "SA1101" = "Missing 'this.' prefix for class members"
    "SA1200" = "Using directive should be placed within namespace"
    "SA1309" = "Field names should not begin with underscore"
    "SA1503" = "Braces should not be omitted"
    "SA1633" = "File should have header with copyright information"
    
    # Code analysis errors
    "CA1031" = "Do not catch general exception types"
    "CA1305" = "Specify IFormatProvider"
    "CA1310" = "Always use StringComparison for string operations"
    "CA1822" = "Mark members as static where possible"
    "CA2000" = "Dispose objects before losing scope"
}

Write-Host "Processing log content..." -ForegroundColor Green

# Function to check if text contains Chinese characters
function ContainsChineseCharacters {
    param([string]$text)
    return $text -match "[\u4E00-\u9FFF]"
}

# Process each line in the log
foreach ($line in ($logContent -split "`r`n")) {
    $line = $line.Trim()
    if ($line -match $errorLinePattern) {
        # Extract error code
        $errorCode = ""
        $errorDescription = ""
        
        if ($line -match "$styleCopPattern\:") {
            $errorCode = $matches[0] -replace "\:"
            $errorDescription = $line -replace ".*$errorCode\:", "" -replace "^\s+"
            
            # Replace Chinese characters with English description
            if (ContainsChineseCharacters -text $errorDescription) {
                if ($compilerErrorDescriptions.ContainsKey($errorCode)) {
                    $errorDescription = $compilerErrorDescriptions[$errorCode]
                } else {
                    $errorDescription = "StyleCop rule violation"
                }
            }
            
            if (-not $styleCopErrors.ContainsKey($errorCode)) {
                $styleCopErrors[$errorCode] = $errorDescription
            }
        }
        elseif ($line -match "$codeAnalysisPattern\:") {
            $errorCode = $matches[0] -replace "\:"
            $errorDescription = $line -replace ".*$errorCode\:", "" -replace "^\s+"
            
            # Replace Chinese characters with English description
            if (ContainsChineseCharacters -text $errorDescription) {
                if ($compilerErrorDescriptions.ContainsKey($errorCode)) {
                    $errorDescription = $compilerErrorDescriptions[$errorCode]
                } else {
                    $errorDescription = "Code Analysis rule violation"
                }
            }
            
            if (-not $codeAnalysisErrors.ContainsKey($errorCode)) {
                $codeAnalysisErrors[$errorCode] = $errorDescription
            }
        }
        elseif ($line -match "$compilerPattern\:") {
            $errorCode = ($matches[0] -replace "error " -replace "\:").Trim()
            $errorDescription = $line -replace ".*$errorCode\:", "" -replace "^\s+"
            
            # Replace with English description if available or if Chinese
            $hasChineseChars = ContainsChineseCharacters -text $errorDescription
            if ($compilerErrorDescriptions.ContainsKey($errorCode) -or $hasChineseChars) {
                if ($compilerErrorDescriptions.ContainsKey($errorCode)) {
                    $errorDescription = $compilerErrorDescriptions[$errorCode]
                } else {
                    $errorDescription = "Compiler error"
                }
            }
            
            # Extract file name for reference
            $fileMatch = $line -match "\[(.*?)\]"
            $fileName = ""
            if ($fileMatch -and $matches.Count -gt 1) {
                $fullPath = $matches[1]
                $fileName = [System.IO.Path]::GetFileName($fullPath)
                $errorDescription += " (in $fileName)"
            }
            
            if (-not $compilerErrors.ContainsKey($errorCode)) {
                $compilerErrors[$errorCode] = $errorDescription
            }
        }
        else {
            # Capture other errors without specific codes
            $errorMatch = $line -match "error\s*\:\s*(.*)"
            if ($errorMatch) {
                $errorDescription = $matches[1]
                
                # Replace Chinese characters
                if (ContainsChineseCharacters -text $errorDescription) {
                    $errorDescription = "General error in build process"
                }
                
                $errorHash = [System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($errorDescription))
                $errorKey = [System.BitConverter]::ToString($errorHash).Replace("-", "").Substring(0, 8)
                
                if (-not $otherErrors.ContainsKey($errorKey)) {
                    $otherErrors[$errorKey] = $errorDescription
                }
            }
        }
    }
}

# Generate markdown report
$reportFilePath = "reports\syntax_analysis_report.md"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

$reportContent = @"
# DDNCadAddins Project - Syntax Analysis Report
Generated: $timestamp

## Summary
This report provides an overview of syntax and code quality issues found during the build process.

"@

# Add StyleCop errors section
$reportContent += @"
## StyleCop Issues
Total unique issues found: $($styleCopErrors.Count)

"@

if ($styleCopErrors.Count -gt 0) {
    $reportContent += "| Error Code | Description |`r`n"
    $reportContent += "|------------|-------------|`r`n"
    foreach ($errorCode in $styleCopErrors.Keys | Sort-Object) {
        $reportContent += "| $errorCode | $($styleCopErrors[$errorCode]) |`r`n"
    }
} else {
    $reportContent += "No StyleCop issues found.`r`n"
}

# Add Code Analysis errors section
$reportContent += @"

## Code Analysis Issues
Total unique issues found: $($codeAnalysisErrors.Count)

"@

if ($codeAnalysisErrors.Count -gt 0) {
    $reportContent += "| Error Code | Description |`r`n"
    $reportContent += "|------------|-------------|`r`n"
    foreach ($errorCode in $codeAnalysisErrors.Keys | Sort-Object) {
        $reportContent += "| $errorCode | $($codeAnalysisErrors[$errorCode]) |`r`n"
    }
} else {
    $reportContent += "No Code Analysis issues found.`r`n"
}

# Add Compiler errors section
$reportContent += @"

## Compiler Errors
Total unique errors found: $($compilerErrors.Count)

"@

if ($compilerErrors.Count -gt 0) {
    $reportContent += "| Error Code | Description |`r`n"
    $reportContent += "|------------|-------------|`r`n"
    foreach ($errorCode in $compilerErrors.Keys | Sort-Object) {
        $reportContent += "| $errorCode | $($compilerErrors[$errorCode]) |`r`n"
    }
} else {
    $reportContent += "No Compiler errors found.`r`n"
}

# Add Other errors section
$reportContent += @"

## Other Issues
Total unique issues found: $($otherErrors.Count)

"@

if ($otherErrors.Count -gt 0) {
    $reportContent += "| ID | Description |`r`n"
    $reportContent += "|---|-------------|`r`n"
    foreach ($errorKey in $otherErrors.Keys | Sort-Object) {
        $reportContent += "| $errorKey | $($otherErrors[$errorKey]) |`r`n"
    }
} else {
    $reportContent += "No other issues found.`r`n"
}

# Common fixes guide
$reportContent += @"

## Common Fixes Guide

### StyleCop Issues (SA)
- **SA1101**: Use 'this.' prefix for class members
- **SA1200**: Using directive should be placed within namespace
- **SA1309**: Field names should not begin with underscore
- **SA1503**: Braces should not be omitted
- **SA1633**: File should have header with copyright information

### Code Analysis Issues (CA)
- **CA1031**: Do not catch general exception types
- **CA1305**: Specify IFormatProvider
- **CA1310**: Always use StringComparison for string operations
- **CA1822**: Mark members as static where possible
- **CA2000**: Dispose objects before losing scope

### Compiler Issues (CS)
- **CS0168**: Variable declared but never used
- **CS0169**: Field never used
- **CS0219**: Variable assigned but never used
- **CS0649**: Field never assigned to
- **CS1591**: Missing XML comment for publicly visible type or member

## Next Steps
1. Review this report to understand the issues
2. Use fix_common_issues.ps1 to automatically fix many issues
3. Manually address remaining issues in your code editor
4. Run build.bat again to verify fixes

"@

# Write report to file using UTF-8 without BOM
try {
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($reportFilePath, $reportContent, $utf8NoBom)
    Write-Host "Report file created successfully: $reportFilePath" -ForegroundColor Green
}
catch {
    Write-Host "Error writing report file: $($_.Exception.Message)" -ForegroundColor Red
    # Fallback to standard Out-File method
    $reportContent | Out-File -FilePath $reportFilePath -Encoding utf8 -Force
}

# Summary
$totalErrors = $styleCopErrors.Count + $codeAnalysisErrors.Count + $compilerErrors.Count + $otherErrors.Count

Write-Host "`nAnalysis complete! Found $totalErrors unique error types:" -ForegroundColor Green
Write-Host "  - StyleCop:     $($styleCopErrors.Count)" -ForegroundColor Cyan
Write-Host "  - Code Analysis: $($codeAnalysisErrors.Count)" -ForegroundColor Cyan
Write-Host "  - Compiler:     $($compilerErrors.Count)" -ForegroundColor Cyan
Write-Host "  - Other:        $($otherErrors.Count)" -ForegroundColor Cyan
Write-Host "`nReport saved to: $reportFilePath" -ForegroundColor Green

if ($totalErrors -gt 0) {
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Review the report to understand the issues" -ForegroundColor Yellow
    Write-Host "2. Run fix_common_issues.ps1 to automatically fix common issues" -ForegroundColor Yellow
    Write-Host "3. Manually fix remaining issues in your code editor" -ForegroundColor Yellow
} else {
    Write-Host "`nCongratulations! No syntax issues found." -ForegroundColor Green
}