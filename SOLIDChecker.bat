@echo off
echo ========================================
echo SOLID原则检查工具 - DDNCadAddins
echo ========================================
echo.

REM 保存当前目录
set CURRENT_DIR=%CD%

REM 设置日期时间变量用于报告文件名
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YYYY=%dt:~0,4%"
set "MM=%dt:~4,2%"
set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%"
set "Min=%dt:~10,2%"
set "Sec=%dt:~12,2%"
set "TIMESTAMP=%YYYY%%MM%%DD%_%HH%%Min%%Sec%"

REM 切换到项目目录
cd /d D:\leaveblackgithub\DDNCadAddins\src\DDNCadAddins\DDNCadAddins

REM 创建报告目录（如果不存在）
if not exist "reports" mkdir reports

echo 正在启动Cursor并执行SOLID原则检查...
echo.

REM 这里使用Cursor的命令行方式打开特定文件并执行SOLID检查
echo 请在Cursor的AI面板中输入以下命令:
echo.
echo "分析当前项目中的代码，检查是否遵循SOLID原则。特别关注:
echo  1. 单一责任原则违反
echo  2. 开闭原则违反
echo  3. 里氏替换原则违反
echo  4. 接口隔离原则违反
echo  5. 依赖倒置原则违反
echo 请详细列出发现的问题和建议的改进方案。
echo 同时将分析结果保存到reports/solid_report_%TIMESTAMP%.md文件中"
echo.

REM 尝试打开Cursor并加载项目
start "" cursor .

echo 提示: 请在Cursor中使用Ctrl+Shift+L打开AI面板，然后粘贴上面的提示进行SOLID原则检查。
echo 检查结果将保存到reports/solid_report_%TIMESTAMP%.md文件中。
echo.

REM 返回原始目录
cd /d %CURRENT_DIR%

pause 