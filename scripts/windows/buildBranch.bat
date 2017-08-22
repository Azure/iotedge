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

set SLN_PATTERN=Microsoft.Azure.*.sln
set CSPROJ_PATTERN=*.csproj
set ANTLR_PATTERN=*.g4
set DOTNET_ROOT_PATH=%AGENT_WORKFOLDER%\dotnet
set PUBLISH_FOLDER=%BUILD_BINARIESDIRECTORY%\publish
set SRC_DOCKER_DIR=%BUILD_REPOSITORY_LOCALPATH%\docker
set RELEASE_TESTS_FOLDER=%BUILD_BINARIESDIRECTORY%\release-tests
set SRC_SCRIPTS_DIR=%BUILD_REPOSITORY_LOCALPATH%\scripts
set TEST_CSPROJ_PATTERN=Microsoft.Azure*Test.csproj
set FUNCTION_BINDING_CSPROJ_PATTERN=*Binding.csproj

if not exist "%BUILD_REPOSITORY_LOCALPATH%" (
    echo Error: %BUILD_REPOSITORY_LOCALPATH% not found
    exit /b 1
)

if not exist "%DOTNET_ROOT_PATH%\dotnet.exe" (
    echo Error: %DOTNET_ROOT_PATH%\dotnet.exe not found
    exit /b 1
)

if exist "%BUILD_BINARIESDIRECTORY%" rd /q /s "%BUILD_BINARIESDIRECTORY%"

echo.
echo Cleaning and restoring all solutions in repo
echo.

for /r %%f in (%SLN_PATTERN%) do (
    echo Cleaning and Restoring packages for solution - %%f
    "%DOTNET_ROOT_PATH%\dotnet" clean %%f
    "%DOTNET_ROOT_PATH%\dotnet" restore %%f
    if !ERRORLEVEL! neq 0 exit /b 1
)

echo.
echo Generating Antlr code files
echo.

set JAVA_COMMAND=java
where %JAVA_COMMAND% >nul 2>&1
if %ERRORLEVEL% neq 0 (
    REM Fallback to using IKVM if Java isn't installed. Java is preferred for Antlr codegen
    REM because it's a lot faster.
    set "JAVA_COMMAND=%UserProfile%\.nuget\packages\Antlr4.CodeGenerator\4.6.1-beta002\tools\ikvm.exe"
)

for /r %%a in (%ANTLR_PATTERN%) do (
    set GENERATED_PATH=%%~dpagenerated
    echo Generating .cs files for - %%a
    if not exist "!GENERATED_PATH!" mkdir "!GENERATED_PATH!"
    "%JAVA_COMMAND%" -jar "%UserProfile%/.nuget/packages/Antlr4.CodeGenerator/4.6.1-beta002/tools/antlr4-csharp-4.6.1-SNAPSHOT-complete.jar" %%a -package Microsoft.Azure.Devices.Routing.Core -Dlanguage=CSharp_v4_5 -visitor -listener -o "!GENERATED_PATH!"
    if !ERRORLEVEL! neq 0 exit /b 1
)

echo.
echo Building all solutions in repo
echo.

for /r %%f in (%SLN_PATTERN%) do (
    echo Building Solution - %%f
    "%DOTNET_ROOT_PATH%\dotnet" build -c %CONFIGURATION% -o "%BUILD_BINARIESDIRECTORY%" %%f
    if !ERRORLEVEL! neq 0 exit /b 1
)

echo.
echo Publishing .NET Core apps
echo.

for /f "usebackq" %%f in (`FINDSTR /spmc:"<OutputType>Exe</OutputType>" %CSPROJ_PATTERN%`) do (
    echo Publishing Solution - %%f
    for %%i in ("%%f") do set PROJ_NAME=%%~ni
    "%DOTNET_ROOT_PATH%\dotnet" publish -f netcoreapp2.0 -c %CONFIGURATION% -o %PUBLISH_FOLDER%\!PROJ_NAME! %%f
    if !ERRORLEVEL! neq 0 exit /b 1
)

for /r %BUILD_REPOSITORY_LOCALPATH% %%f in (%FUNCTION_BINDING_CSPROJ_PATTERN%) do (
        echo Publishing - %%f
        for %%i in ("%%f") do set PROJ_NAME=%%~ni
      
        "%DOTNET_ROOT_PATH%\dotnet" publish -f netstandard2.0 -c %CONFIGURATION% -o %PUBLISH_FOLDER%\!PROJ_NAME! %%f
        if !ERRORLEVEL! neq 0 exit /b 1
    )

echo Copying %SRC_DOCKER_DIR% to %PUBLISH_FOLDER%\docker
xcopy /si %SRC_DOCKER_DIR% %PUBLISH_FOLDER%\docker

echo Publish tests - %PUBLISH_TESTS%
if "!PUBLISH_TESTS!" == "--publish-tests"  (
    echo.
    echo Publishing .NET Core Tests
    echo.

    for /r %BUILD_REPOSITORY_LOCALPATH% %%f in (%TEST_CSPROJ_PATTERN%) do (
        echo Publishing - %%f to %RELEASE_TESTS_FOLDER%\!PROJ_NAME!
        for %%i in ("%%f") do set PROJ_NAME=%%~ni
        "%DOTNET_ROOT_PATH%\dotnet" publish -f netcoreapp2.0 -c %CONFIGURATION% -o %RELEASE_TESTS_FOLDER%\target %%f
        if !ERRORLEVEL! neq 0 exit /b 1

        echo "Copying %%f to %RELEASE_TESTS_FOLDER%\!PROJ_NAME!\"
        xcopy %%f "%RELEASE_TESTS_FOLDER%\!PROJ_NAME!\"
    )

    echo Copying %SRC_SCRIPTS_DIR% to %RELEASE_TESTS_FOLDER%
    xcopy /si %SRC_SCRIPTS_DIR% %RELEASE_TESTS_FOLDER%\scripts
)
