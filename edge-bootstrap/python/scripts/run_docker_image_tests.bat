@echo off
@setlocal EnableExtensions EnableDelayedExpansion

REM ###########################################################################
REM This script builds the azure-iot-edge-runtime-ctl test images
REM ###########################################################################

set EXE_DIR=%~dp0\..

REM Determine the docker engine OS to determine what docker test images to build and execute
for /f %%i in ('docker info --format "{{lower .OSType}}"') do set OS_TYPE=%%i
if !ERRORLEVEL! NEQ 0 goto test_error


echo Docker engine OS type: %OS_TYPE%

REM Note: python 2.x tests are disabled for Windows because of no embedded python distribution
REM is available for installation in a nanoserver image. For python 2.x images there is a public
REM windowsservercore image available which is 6GB and has proven to be flaky to download and run.
if "%OS_TYPE%" == "linux" (
    echo.
    echo ###########################################################################################
    echo Building python 2.7.14 image...
    echo ###########################################################################################
    docker build -t iotedgectl_py2 --file %EXE_DIR%\docker\python2\%OS_TYPE%\Dockerfile %EXE_DIR%
    if !ERRORLEVEL! NEQ 0 goto test_error
)

echo.
echo #############################################################################################
echo Building python 3.x image...
echo #############################################################################################
docker build -t iotedgectl_py3 --file %EXE_DIR%\docker\python3\%OS_TYPE%\Dockerfile %EXE_DIR%
if !ERRORLEVEL! NEQ 0 goto test_error

if "%OS_TYPE%" == "linux" (
    echo.
    echo ###########################################################################################
    echo Executing 2.x Tests...
    echo ###########################################################################################
    docker run --rm iotedgectl_py2
    if !ERRORLEVEL! NEQ 0 goto test_error
)

echo.
echo #############################################################################################
echo Executing 3.x Tests...
echo #############################################################################################
docker run --rm iotedgectl_py3
if !ERRORLEVEL! NEQ 0 goto test_error

echo.
echo #############################################################################################
echo Cleanup test images
echo #############################################################################################
if "%OS_TYPE%" == "linux" (
    docker rmi iotedgectl_py2
    if !ERRORLEVEL! NEQ 0 goto test_error
)

docker rmi iotedgectl_py3
if !ERRORLEVEL! NEQ 0 goto test_error

goto test_end

:test_error
    echo Test errors seen.
    exit /B 1

:test_end
    echo Test Done.
