@echo off
REM ============================================================
REM Code RAG MCP Server - 安装脚本
REM ============================================================
REM 功能：
REM   1. 创建 Python 虚拟环境
REM   2. 安装依赖包
REM   3. 验证安装
REM ============================================================

setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "VENV_DIR=%SCRIPT_DIR%\.venv"

echo ============================================================
echo  Code RAG MCP Server 安装程序
echo ============================================================
echo.

REM 检查 Python
python --version >nul 2>&1
if errorlevel 1 (
    echo [错误] 未找到 Python，请先安装 Python 3.11+
    exit /b 1
)

echo [1/4] 检查 Python 版本...
python --version
echo.

REM 创建虚拟环境
if not exist "%VENV_DIR%" (
    echo [2/4] 创建 Python 虚拟环境...
    python -m venv "%VENV_DIR%"
    if errorlevel 1 (
        echo [错误] 创建虚拟环境失败
        exit /b 1
    )
    echo 虚拟环境已创建: %VENV_DIR%
) else (
    echo [2/4] 虚拟环境已存在，跳过创建
)
echo.

REM 升级 pip
echo [3/4] 升级 pip...
"%VENV_DIR%\Scripts\python.exe" -m pip install --upgrade pip
echo.

REM 安装依赖
echo [4/4] 安装依赖包（首次安装可能需要几分钟，正在下载模型和库）...
"%VENV_DIR%\Scripts\pip.exe" install -r "%SCRIPT_DIR%\requirements.txt"
if errorlevel 1 (
    echo [错误] 依赖安装失败
    echo 请检查网络连接，或手动运行: pip install -r requirements.txt
    exit /b 1
)
echo.

REM 验证安装
echo ============================================================
echo  验证安装
echo ============================================================
"%VENV_DIR%\Scripts\python.exe" -c "import mcp; import sentence_transformers; import chromadb; print('所有依赖安装成功!'); print(f'  mcp: {mcp.__version__}'); print(f'  sentence-transformers: OK'); print(f'  chromadb: {chromadb.__version__}')"

if errorlevel 1 (
    echo [错误] 依赖验证失败
    exit /b 1
)

echo.
echo ============================================================
echo  安装完成!
echo ============================================================
echo.
echo  虚拟环境路径: %VENV_DIR%
echo  服务器脚本:   %SCRIPT_DIR%\server.py
echo.
echo  MCP 配置已写入 .roo\mcp.json
echo  重启 Zoo Code 后，RAG 工具将自动可用。
echo.
echo  使用流程:
echo    1. 在 Zoo Code 中调用 index_codebase 索引代码
echo    2. 使用 search_code 进行语义搜索
echo    3. 使用 get_code_context 获取代码上下文
echo.
pause
