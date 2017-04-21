@echo off

REM This Script builds all .Net Core Solutions in the repo by recursing through
REM the repo and finding any *.sln files
REM This script expects that .Net Core is installed at %AGENT_WORKFOLDER%\dotnet
REM by a previous step.

IF NOT DEFINED BUILD_REPOSITORY_LOCALPATH (
	ECHO Error: BUILD_REPOSITORY_LOCALPATH needs to be set to the root of the repository.
	EXIT /B 1
)

IF NOT DEFINED AGENT_WORKFOLDER (
	ECHO Error: AGENT_WORKFOLDER needs to be set to the working folder where .Net Core is installed.
	EXIT /B 1
)

IF NOT DEFINED BUILD_BINARIESDIRECTORY (
	ECHO Error: BUILD_BINARIESDIRECTORY needs to be set to the folder where output binaries will be placed.
	EXIT /B 1
)

SET SUFFIX=Microsoft.Azure.*.sln
SET ROOTFOLDER=%BUILD_REPOSITORY_LOCALPATH%
SET DOTNET_ROOT_PATH=%AGENT_WORKFOLDER%\dotnet
SET OUTPUT_FOLDER=%BUILD_BINARIESDIRECTORY%

IF NOT EXIST %ROOTFOLDER% (
	ECHO Error: %ROOTFOLDER% not found
	EXIT /B 1
)

IF NOT EXIST %DOTNET_ROOT_PATH%\dotnet.exe (
	ECHO Error: %DOTNET_ROOT_PATH%\dotnet.exe not found
	EXIT /B 1
)

IF NOT EXIST %OUTPUT_FOLDER% (
	MKDIR %OUTPUT_FOLDER%
)

ECHO Building all solutions in repo

IF EXIST %OUTPUT_FOLDER% RD /q /s %OUTPUT_FOLDER%
FOR /R %%f IN (%SUFFIX%) DO (
    ECHO Building Solution - %%f
    %DOTNET_ROOT_PATH%\dotnet clean %%f
    %DOTNET_ROOT_PATH%\dotnet restore %%f
    %DOTNET_ROOT_PATH%\dotnet build %%f -o %OUTPUT_FOLDER%
)

EXIT /B %ERRORLEVEL%

:echoError
ECHO %*
