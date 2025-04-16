@echo off
chcp 65001 > nul
echo Updating version number...
powershell -ExecutionPolicy Bypass -File "%~dp0UpdateVersion.ps1"
echo Building project...
dotnet build "%~dp0DDNCadAddins.csproj"
echo Build completed! 