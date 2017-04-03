@echo off

REM This script runs all the .Net Core test projects (*test*.csproj) in the
REM repo by recursing from the repo root. 
REM This script expects that .Net Core is installed at 
REM %AGENT_WORKFOLDER%\dotnet and output binaries are at %BUILD_BINARIESDIRECTORY%

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

SET TEST_PROJ_PATTERN=*test*.csproj
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

ECHO Running tests in all Test Projects in repo
FOR /R %ROOTFOLDER% %%f IN (%TEST_PROJ_PATTERN%) DO (
    ECHO Running tests for project - %%f
    %DOTNET_ROOT_PATH%/dotnet test --logger "trx;LogFileName=result.trx" -o %OUTPUT_FOLDER% --no-build %%f
)
