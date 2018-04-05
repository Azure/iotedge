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

set TAG_BASE=iotedgectl_py
if "%OS_TYPE%" == "linux" (
    set BASE_VERSION_LIST=2.7.14-jessie 3.4.8-jessie 3.5.5-jessie 3.6.4-jessie 3.6.5-jessie 3.7.0b2-stretch
) else (
    REM Note: python 2.x tests are disabled for Windows because of no embedded python distribution
    REM is available for installation in a nanoserver image. For python 2.x images there is a public
    REM windowsservercore image available which is 6GB and has proven to be flaky to download and run.
    set BASE_VERSION_LIST=3.5.4 3.6.4 3.6.5 3.7.0b2
)

REM Build images
(for %%v in (%BASE_VERSION_LIST%) do (
    echo.
    echo ###########################################################################################
    echo Building python %%v image...
    echo ###########################################################################################
    docker build --build-arg BASE_VERSION=%%v -t %TAG_BASE%%%v --file %EXE_DIR%\docker\%OS_TYPE%\Dockerfile %EXE_DIR%
    if !ERRORLEVEL! NEQ 0 goto test_error
))

REM Create containers and run tests
(for %%v in (%BASE_VERSION_LIST%) do (
    echo.
    echo ###########################################################################################
    echo Executing python %%v Tests...
    echo ###########################################################################################
    docker run --rm %TAG_BASE%%%v
    if !ERRORLEVEL! NEQ 0 goto test_error
))

REM Remove images
(for %%v in (%BASE_VERSION_LIST%) do (
    echo.
    echo ###########################################################################################
    echo Executing python %%v Tests...
    echo ###########################################################################################
    docker rmi %TAG_BASE%%%v
    if !ERRORLEVEL! NEQ 0 goto test_error
))

goto test_end

:test_error
    echo Test errors seen.
    exit /B 1

:test_end
    echo Test Done.
