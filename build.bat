@echo off
echo DDNCadAddins Project - Build and Syntax Checker
echo ==============================================

REM Create reports directory if it doesn't exist
if not exist reports mkdir reports

REM Set UTF-8 encoding for output
chcp 65001 > nul

REM Clean and build solution with analyzers enabled
echo Running .NET build with analyzers...
cd src\DDNCadAddins
dotnet clean > nul
dotnet build /p:RunAnalyzers=true /p:RunAnalyzersDuringBuild=true /p:TreatWarningsAsErrors=true > ..\..\reports\syntax_report.log 2>&1
cd ..\..

REM Check if there were any errors during the build
find "error" reports\syntax_report.log > nul
if not errorlevel 1 (
    echo Errors found during build. Analyzing and fixing issues...
    
    REM Run PowerShell scripts with UTF-8 encoding
    powershell -ExecutionPolicy Bypass -Command "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '.\analyze_syntax.ps1'"
    
    echo.
    echo Automatically running fix tool...
    powershell -ExecutionPolicy Bypass -Command "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & '.\fix_common_issues.ps1'"
    
    echo.
    echo Fix process completed. Check results in reports\syntax_analysis_report.md
    echo Run this script again to verify all errors are fixed.
) else (
    echo No errors found. Build successful!
    echo.
    echo Project built successfully with no syntax issues detected.
)

echo.
echo Build process completed. 
