@echo off
@setlocal EnableExtensions EnableDelayedExpansion

REM This script runs all the IoT Edge Ctl unit and integration tests.
REM This script expects that docker, python and pip are available and installed.

REM Get directory of running script
set ROOTFOLDER=%~dp0..\..
set IOTEDGECTL_DIR=%ROOTFOLDER%\edge-bootstrap\python
set RESULT=0

echo Running iotedgectl tests...
set test_cmd=%IOTEDGECTL_DIR%\scripts\run_docker_image_tests.bat
echo Executing iotedgectl tests command %test_cmd%

%test_cmd%
IF !ERRORLEVEL! NEQ 0 (
    set RESULT=1
    echo Failed iotedgectl test: %test_cmd%
)

echo iotedgectl tests result: %RESULT%
exit /B %RESULT%