@echo off
chcp 65001 > nul
echo 正在更新版本号...
powershell -ExecutionPolicy Bypass -File "%~dp0UpdateVersion.ps1"
echo 正在构建项目...
dotnet build
echo 构建完成! 