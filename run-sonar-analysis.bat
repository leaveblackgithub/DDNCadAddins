@echo off
echo 开始SonarQube分析...

rem 设置SonarQube服务器信息 - 请根据实际情况修改
set SONAR_HOST_URL=http://localhost:9000
set SONAR_LOGIN=your_token_here

rem 安装SonarScanner .NET工具（如果尚未安装）
dotnet tool install --global dotnet-sonarscanner

rem 开始SonarQube分析
dotnet sonarscanner begin /k:"DDNCadAddins" /n:"DDNCadAddins" /v:"1.0" /d:sonar.host.url="%SONAR_HOST_URL%" /d:sonar.login="%SONAR_LOGIN%" /d:sonar.cs.file.suffixes=.cs /d:sonar.cs.roslyn.ignoreIssues=false

rem 构建项目
dotnet build src\DDNCadAddins.sln --configuration Release

rem 结束SonarQube分析
dotnet sonarscanner end /d:sonar.login="%SONAR_LOGIN%"

echo SonarQube分析完成
echo 请访问 %SONAR_HOST_URL% 查看分析结果
pause 