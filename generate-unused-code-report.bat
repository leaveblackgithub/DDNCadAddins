@echo off
echo 正在生成未使用代码报告...

rem 设置SonarQube服务器信息 - 请根据实际情况修改
set SONAR_HOST_URL=http://localhost:9000
set SONAR_LOGIN=your_token_here
set PROJECT_KEY=DDNCadAddins

rem 使用curl从SonarQube API获取未使用代码的问题
curl -s -u "%SONAR_LOGIN%:" "%SONAR_HOST_URL%/api/issues/search?componentKeys=%PROJECT_KEY%&rules=csharpsquid:S1144,csharpsquid:S1186,csharpsquid:S1172&ps=500" > unused_code_report.json

rem 使用PowerShell解析JSON并生成报告
powershell -Command "& {$json = Get-Content unused_code_report.json | ConvertFrom-Json; $issues = $json.issues; $report = @(); foreach($issue in $issues) { $report += [PSCustomObject]@{Component=$issue.component; Message=$issue.message; Line=$issue.line; Path=$issue.component.Replace('%PROJECT_KEY%:', '')}; }; $report | Format-Table -AutoSize | Out-File -FilePath unused_code_report.txt -Encoding utf8; Write-Host '找到' $report.Count '个未使用的代码问题';}"

echo 报告生成完成，请查看 unused_code_report.txt

pause 