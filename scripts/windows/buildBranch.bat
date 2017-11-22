@echo off
@setlocal EnableExtensions EnableDelayedExpansion

REM This script finds and builds all .NET Core solutions in the repo, and
REM publishes .NET Core apps to target/publish/.

REM Process script arguments
IF NOT [%1] == [] (
    SET "PUBLISH_TESTS=%~1"
)

if not defined BUILD_REPOSITORY_LOCALPATH (
    set BUILD_REPOSITORY_LOCALPATH=%~dp0..\..
)

if not defined AGENT_WORKFOLDER (	
    set AGENT_WORKFOLDER=%ProgramFiles%
)

if not defined BUILD_BINARIESDIRECTORY (
    set BUILD_BINARIESDIRECTORY=%BUILD_REPOSITORY_LOCALPATH%\target
)

if not defined CONFIGURATION (
    set CONFIGURATION=Debug
)

if not defined BUILD_SOURCEVERSION (
	set BUILD_SOURCEVERSION=''
)

if not defined BUILD_BUILDID (
	set BUILD_BUILDID=''
)

set SLN_PATTERN=Microsoft.Azure.*.sln
set CSPROJ_PATTERN=*.csproj
set DOTNET_ROOT_PATH=%AGENT_WORKFOLDER%\dotnet
set PUBLISH_FOLDER=%BUILD_BINARIESDIRECTORY%\publish
set SRC_DOCKER_DIR=%BUILD_REPOSITORY_LOCALPATH%\docker
set RELEASE_TESTS_FOLDER=%BUILD_BINARIESDIRECTORY%\release-tests
set SRC_SCRIPTS_DIR=%BUILD_REPOSITORY_LOCALPATH%\scripts
set SRC_BIN_DIR=%BUILD_REPOSITORY_LOCALPATH%\bin
set TEST_CSPROJ_PATTERN=Microsoft.Azure*Test.csproj
set FUNCTION_BINDING_CSPROJ_PATTERN=*Binding.csproj
set VERSIONINFO_FILE_PATH=%BUILD_REPOSITORY_LOCALPATH%\versionInfo.json

if not exist "%BUILD_REPOSITORY_LOCALPATH%" (
    echo Error: %BUILD_REPOSITORY_LOCALPATH% not found
    exit /b 1
)

if not exist "%DOTNET_ROOT_PATH%\dotnet.exe" (
    echo Error: %DOTNET_ROOT_PATH%\dotnet.exe not found
    exit /b 1
)

if exist "%BUILD_BINARIESDIRECTORY%" rd /q /s "%BUILD_BINARIESDIRECTORY%"

if exist "%VERSIONINFO_FILE_PATH%" (

	echo.
	echo Updating versionInfo.json file with the build ID and commit ID.
	echo.

	powershell -Command "(gc %VERSIONINFO_FILE_PATH%) -replace 'BUILDNUMBER', %BUILD_BUILDID% | Out-File %VERSIONINFO_FILE_PATH%"
	powershell -Command "(gc %VERSIONINFO_FILE_PATH%) -replace 'COMMITID', %BUILD_SOURCEVERSION% | Out-File %VERSIONINFO_FILE_PATH%"

) else (

	echo.
	echo VersionInfo.json file not found. 
	echo.

)

SET RES=0

echo.
echo Cleaning and restoring all solutions in repo
echo.

for /r %%f in (%SLN_PATTERN%) do (
    echo Cleaning and Restoring packages for solution - %%f
    "%DOTNET_ROOT_PATH%\dotnet" clean %%f
    "%DOTNET_ROOT_PATH%\dotnet" restore %%f
    IF !ERRORLEVEL! NEQ 0 (
        SET RES=!ERRORLEVEL!
        GOTO END
    )
)

echo.
echo Building all solutions in repo
echo.

for /r %%f in (%SLN_PATTERN%) do (
    echo Building Solution - %%f
    "%DOTNET_ROOT_PATH%\dotnet" build -c %CONFIGURATION% -o "%BUILD_BINARIESDIRECTORY%" %%f
    IF !ERRORLEVEL! NEQ 0 (
        SET RES=!ERRORLEVEL!
        GOTO END
    )
)

echo.
echo Publishing .NET Core apps
echo.

for /f "usebackq" %%f in (`FINDSTR /spmc:"<OutputType>Exe</OutputType>" %CSPROJ_PATTERN%`) do (
    echo Publishing Solution - %%f
    for %%i in ("%%f") do set PROJ_NAME=%%~ni
    "%DOTNET_ROOT_PATH%\dotnet" publish -f netcoreapp2.0 -c %CONFIGURATION% -o %PUBLISH_FOLDER%\!PROJ_NAME! %%f
    IF !ERRORLEVEL! NEQ 0 (
        SET RES=!ERRORLEVEL!
        GOTO END
    )
)

for /r %BUILD_REPOSITORY_LOCALPATH% %%f in (%FUNCTION_BINDING_CSPROJ_PATTERN%) do (
    echo Publishing - %%f
    for %%i in ("%%f") do set PROJ_NAME=%%~ni

    "%DOTNET_ROOT_PATH%\dotnet" publish -f netstandard2.0 -c %CONFIGURATION% -o %PUBLISH_FOLDER%\!PROJ_NAME! %%f
    IF !ERRORLEVEL! NEQ 0 (
        SET RES=!ERRORLEVEL!
        GOTO END
    )
)

echo Copying %SRC_DOCKER_DIR% to %PUBLISH_FOLDER%\docker
xcopy /si %SRC_DOCKER_DIR% %PUBLISH_FOLDER%\docker

echo Copying %SRC_SCRIPTS_DIR% to %PUBLISH_FOLDER%
xcopy /si %SRC_SCRIPTS_DIR% %PUBLISH_FOLDER%\scripts

echo Copying %SRC_BIN_DIR% to %PUBLISH_FOLDER%
xcopy /si %SRC_BIN_DIR% %PUBLISH_FOLDER%\bin

echo Publish tests - %PUBLISH_TESTS%
if "!PUBLISH_TESTS!" == "--publish-tests"  (
    echo.
    echo Publishing .NET Core Tests
    echo.

    for /r %BUILD_REPOSITORY_LOCALPATH% %%f in (%TEST_CSPROJ_PATTERN%) do (
        echo Publishing - %%f to %RELEASE_TESTS_FOLDER%\!PROJ_NAME!
        for %%i in ("%%f") do set PROJ_NAME=%%~ni
        "%DOTNET_ROOT_PATH%\dotnet" publish -f netcoreapp2.0 -c %CONFIGURATION% -o %RELEASE_TESTS_FOLDER%\target %%f
        IF !ERRORLEVEL! NEQ 0 (
            SET RES=!ERRORLEVEL!
            GOTO END
        )

        echo "Copying %%f to %RELEASE_TESTS_FOLDER%\!PROJ_NAME!\"
        xcopy %%f "%RELEASE_TESTS_FOLDER%\!PROJ_NAME!\"
    )

    echo Copying %SRC_SCRIPTS_DIR% to %RELEASE_TESTS_FOLDER%
    xcopy /si %SRC_SCRIPTS_DIR% %RELEASE_TESTS_FOLDER%\scripts
    xcopy %BUILD_REPOSITORY_LOCALPATH%\Nuget.config %RELEASE_TESTS_FOLDER%
)

:END

exit /b %RES%