@echo off
@setlocal EnableExtensions EnableDelayedExpansion

REM This script runs all the .Net Core test projects (*test*.csproj) in the
REM repo by recursing from the repo root.
REM This script expects that .Net Core is installed at
REM %AGENT_WORKFOLDER%\dotnet and output binaries are at %BUILD_BINARIESDIRECTORY%

IF NOT DEFINED BUILD_REPOSITORY_LOCALPATH (
    SET BUILD_REPOSITORY_LOCALPATH=%~dp0..\..
)

IF NOT DEFINED AGENT_WORKFOLDER (
    SET AGENT_WORKFOLDER=%ProgramFiles%
)

IF NOT DEFINED BUILD_BINARIESDIRECTORY (
    SET BUILD_BINARIESDIRECTORY=%BUILD_REPOSITORY_LOCALPATH%\target
)

SET TEST_PROJ_PATTERN=Microsoft.Azure*test*.csproj
SET ROOTFOLDER=%BUILD_REPOSITORY_LOCALPATH%
SET DOTNET_ROOT_PATH=%AGENT_WORKFOLDER%\dotnet
SET OUTPUT_FOLDER=%BUILD_BINARIESDIRECTORY%

IF NOT EXIST "%ROOTFOLDER%" (
    ECHO Error: %ROOTFOLDER% not found
    EXIT /B 1
)

IF NOT EXIST "%DOTNET_ROOT_PATH%\dotnet.exe" (
    ECHO Error: %DOTNET_ROOT_PATH%\dotnet.exe not found
    EXIT /B 1
)

IF NOT EXIST "%OUTPUT_FOLDER%" (
    MKDIR "%OUTPUT_FOLDER%"
)

SET opencover=%ROOTFOLDER%\OpenCover.4.6.519\tools\OpenCover.Console.exe
SET targetargs=test %1 --logger trx;LogFileName=result.trx

ECHO Running tests in all Test Projects in repo
FOR /R %%f IN (%TEST_PROJ_PATTERN%) DO (
    ECHO Running tests for project - %%f
    IF EXIST "%opencover%" (
        "%opencover%" -register:user -target:%DOTNET_ROOT_PATH%/dotnet.exe -targetargs:"%targetargs% %%f" -skipautoprops -hideskipped:All  -oldstyle -output:%OUTPUT_FOLDER%\code-coverage.xml -mergeoutput:%OUTPUT_FOLDER%\code-coverage.xml
    ) ELSE (
        "%DOTNET_ROOT_PATH%\dotnet" test %%f --logger "trx;LogFileName=result.trx" -o "%OUTPUT_FOLDER%" --no-build --filter Category!=Bvt
    )
)

IF EXIST "%ROOTFOLDER%\OpenCoverToCoberturaConverter.0.2.6.0\tools\OpenCoverToCoberturaConverter.exe" (
    "%ROOTFOLDER%\OpenCoverToCoberturaConverter.0.2.6.0\tools\OpenCoverToCoberturaConverter.exe" -input:%OUTPUT_FOLDER%\code-coverage.xml -output:%OUTPUT_FOLDER%\CoberturaCoverage.xml -sources:.
)

IF EXIST "%ROOTFOLDER%\ReportGenerator.2.5.6\tools\ReportGenerator.exe" (
    "%ROOTFOLDER%\ReportGenerator.2.5.6\tools\ReportGenerator.exe" -reporttypes:MHtml -reports:%OUTPUT_FOLDER%\code-coverage.xml -targetdir:%OUTPUT_FOLDER%\Report
)
