@echo off
setlocal EnableExtensions EnableDelayedExpansion

if "%1"=="/?" goto usage
if "%1"=="-h" goto usage
if "%1"=="--help" goto usage

if not defined cnreg_pull_user goto missingvars
if not defined cnreg_pull_pass goto missingvars
if not defined cnreg_push_user goto missingvars
if not defined cnreg_push_pass goto missingvars

set "cnreg_pull_name=edgebuilds.azurecr.io"
set "cnreg_push_name=azureiotedgeprivatepreview.azurecr.io"

where docker > nul 2>&1
if ERRORLEVEL 1 goto nodocker

rem Query Docker to find out whether it's configured for Linux or Windows containers
for /f "usebackq tokens=2 delims=: " %%i in (`docker info ^| find /i "ostype"`) do set dockeros=%%i

set pushos=%1
if "%pushos%"=="" set pushos=linux
if not "%pushos%"=="linux" if not "%pushos%"=="windows" goto containermismatch
if "%pushos%"=="linux" if not "%dockeros%"=="linux" goto osmismatch
if "%pushos%"=="windows" if not "%dockeros%"=="windows" goto osmismatch

if "%pushos%"=="windows" (
    set "image[0]=azureiotedge/edge-service-windows-amd64"
    set "image[1]=azureiotedge/simulated-temperature-sensor-windows-amd64"
) else (
    set "image[0]=azureiotedge/edge-service-amd64"
    set "image[1]=azureiotedge/simulated-temperature-sensor-amd64"
    set "image[2]=azureiotedge/edge-service-arm32v7"
    set "image[3]=azureiotedge/simulated-temperature-sensor-arm32v7"
)

echo.
echo Login to container registries...

echo docker login -u %cnreg_pull_user% -p %cnreg_pull_pass% %cnreg_pull_name%
if ERRORLEVEL 1 exit /b %ERRORLEVEL%

echo docker login -u %cnreg_push_user% -p %cnreg_push_pass% %cnreg_push_name%
if ERRORLEVEL 1 exit /b %ERRORLEVEL%

echo.
echo Pull images from %cnreg_pull_name%...

for /l %%i in (0,1,9) do (
    if defined image[%%i] (
        docker pull %cnreg_pull_name%/!image[%%i]!:latest
        if ERRORLEVEL 1 exit /b %ERRORLEVEL%
    )
)

for /l %%i in (0,1,9) do (
    if defined image[%%i] (
        docker tag %cnreg_pull_name%/!image[%%i]!:latest %cnreg_push_name%/!image[%%i]!:latest
        if ERRORLEVEL 1 exit /b %ERRORLEVEL%
    )
)

echo.
echo Ready to push the following images:
for /l %%i in (0,1,9) do (
    if defined image[%%i] (
        echo %cnreg_push_name%/!image[%%i]!:latest
    )
)
choice /c YN /m "Continue?"
if %errorlevel% neq 1 (
    echo Aborting...
    exit /b 1
)
rem Reset error level to 0
type nul

for /l %%i in (0,1,9) do (
    if defined image[%%i] (
        docker push %cnreg_push_name%/!image[%%i]!:latest
        if ERRORLEVEL 1 exit /b %ERRORLEVEL%
    )
)

echo.
echo Done.

goto :eof

:missingvars
echo ERROR: Environment variables missing. Make sure you have defined
echo        CNREG_PULL_USER - username to access the source container registry
echo        CNREG_PULL_PASS - password to access the source container registry
echo        CNREG_PUSH_USER - username to access the destination container registry
echo        CNREG_PUSH_PASS - password to access the destination container registry
exit /b 1

:nodocker
echo ERROR: Couldn't find Docker.exe. Exiting...
exit /b 1

:osmismatch
echo ERROR: Trying to push %pushos% containers, but Docker is configured for %dockeros% containers. Exiting...
exit /b 1

:containermismatch
echo ERROR: Unrecognized container type specified on the command line. Use 'linux' or 'windows'. Exiting...
exit /b 1

:usage
echo pushPrivatePreview.cmd [/?^|-h^|--help] [*linux^|windows]
goto :eof