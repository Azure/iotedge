@echo off
REM ###########################################################################
REM This script builds the azure-iot-edge-runtime-ctl test images
REM ###########################################################################

set EXE_DIR=%~dp0\..
echo Exe Dir %EXE_DIR%

echo.
echo #############################################################################################
echo Building python 2.7.14 Linux image...
echo #############################################################################################
docker build -t iotedgectl_py2 --file %EXE_DIR%\docker\python2\linux\Dockerfile %EXE_DIR%
if %errorlevel% NEQ 0 goto test_error

echo.
echo #############################################################################################
echo Building python 3.x Linux image...
echo #############################################################################################
docker build -t iotedgectl_py3 --file %EXE_DIR%\docker\python3\linux\Dockerfile %EXE_DIR%
if %errorlevel% NEQ 0 goto test_error

echo.
echo #############################################################################################
echo Executing 2.x Tests...
echo #############################################################################################
docker run --rm iotedgectl_py2
if %errorlevel% NEQ 0 goto test_error

echo.
echo #############################################################################################
echo Executing 3.x Tests...
echo #############################################################################################
docker run --rm iotedgectl_py3
if %errorlevel% NEQ 0 goto test_error

echo.
echo #############################################################################################
echo Cleanup test images
echo #############################################################################################
docker rmi iotedgectl_py2
if %errorlevel% NEQ 0 goto test_error

docker rmi iotedgectl_py3
if %errorlevel% NEQ 0 goto test_error

goto test_end

:test_error
    echo Test errors seen.
    exit /B 1

:test_end
    echo Test Done.
