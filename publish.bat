@echo off
chcp 65001 >nul
echo ============================================
echo   奥格快捷收纳箱 - 一键发布
echo ============================================
echo.

echo [1/4] 关闭正在运行的 ShortcutOrganizer...
taskkill /F /IM ShortcutOrganizer.exe 2>nul

echo.
echo [2/4] 删除旧的 publish 目录...
if exist publish rmdir /S /Q publish

echo.
echo [3/4] 发布自包含单文件...
dotnet publish -c Release -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -o ./publish

if errorlevel 1 (
    echo.
    echo ❌ 发布失败！
    pause
    exit /b 1
)

echo.
echo [4/4] 发布完成！文件位置：
echo    %cd%\publish\ShortcutOrganizer.exe

echo.
echo 文件大小：
for %%A in (publish\ShortcutOrganizer.exe) do echo    %%~zA 字节

echo.
echo 按任意键退出...
pause >nul