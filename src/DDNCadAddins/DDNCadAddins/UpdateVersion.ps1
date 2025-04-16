# Version Update Script
param (
    [string]$assemblyInfoPath = "Properties\AssemblyInfo.cs"
)

# Set output encoding to UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Get full path
$fullPath = Join-Path $PSScriptRoot $assemblyInfoPath
Write-Host "Updating version number: $fullPath"

# Read file content
$content = Get-Content $fullPath -Raw

# Find version number
$versionPattern = '\[assembly: AssemblyVersion\("(\d+)\.(\d+)\.(\d+)\.(\d+)"\)\]'
$versionMatch = [regex]::Match($content, $versionPattern)

if ($versionMatch.Success) {
    # Extract current version number
    $major = [int]$versionMatch.Groups[1].Value
    $minor = [int]$versionMatch.Groups[2].Value
    $build = [int]$versionMatch.Groups[3].Value
    $revision = [int]$versionMatch.Groups[4].Value + 1
    
    # Create new version number
    $newVersion = "$major.$minor.$build.$revision"
    
    Write-Host "Old version: $major.$minor.$build.$($revision-1)"
    Write-Host "New version: $newVersion"
    
    # Update AssemblyVersion
    $newAssemblyVersion = "[assembly: AssemblyVersion(""$newVersion"")]"
    $content = [regex]::Replace($content, $versionPattern, $newAssemblyVersion)
    
    # Update AssemblyFileVersion
    $fileVersionPattern = '\[assembly: AssemblyFileVersion\("(\d+)\.(\d+)\.(\d+)\.(\d+)"\)\]'
    $newFileVersion = "[assembly: AssemblyFileVersion(""$newVersion"")]"
    $content = [regex]::Replace($content, $fileVersionPattern, $newFileVersion)
    
    # Write back to file
    Set-Content -Path $fullPath -Value $content
    
    Write-Host "Version number updated to $newVersion"
} else {
    Write-Host "Version number information not found"
} 