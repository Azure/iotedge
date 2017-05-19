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
SET ANTLRSUFFIX=*.g4
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

ECHO Cleaning and restoring all solutions in repo

FOR /R %%f IN (%SUFFIX%) DO (
    ECHO Cleaning and Restoring packages for solution - %%f
    %DOTNET_ROOT_PATH%\dotnet clean %%f
    %DOTNET_ROOT_PATH%\dotnet restore %%f
)

ECHO Generate Antlr code files
SET JAVACOMMAND=java
where %JAVACOMMAND%
if %ERRORLEVEL% NEQ 0 (
	SET JAVACOMMAND=%UserProfile%/.nuget/packages/Antlr4.CodeGenerator/4.6.1-beta002/tools/ikvm.exe
)

FOR /R %%f IN (%ANTLRSUFFIX%) DO (
    ECHO Generating .cs files for - %%f
    %JAVACOMMAND% -jar %UserProfile%/.nuget/packages/Antlr4.CodeGenerator/4.6.1-beta002/tools/antlr4-csharp-4.6.1-SNAPSHOT-complete.jar %%f -package Microsoft.Azure.Devices.Routing.Core -Dlanguage=CSharp_v4_5 -visitor -listener
)

ECHO Building all solutions in repo

SET RES=0
IF EXIST %OUTPUT_FOLDER% RD /q /s %OUTPUT_FOLDER%
FOR /R %%f IN (%SUFFIX%) DO (
    ECHO Building Solution - %%f
    %DOTNET_ROOT_PATH%\dotnet build %%f -o %OUTPUT_FOLDER%
	IF %ERRORLEVEL% NEQ 0 (
		SET RES=1
	)
)

EXIT /B %RES%

:echoError
ECHO %*
