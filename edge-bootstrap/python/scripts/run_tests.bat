@echo off
@setlocal EnableExtensions EnableDelayedExpansion

REM ###########################################################################
REM This script executes the azure-iot-edge-runtime-ctl tests
REM Arg(s):
REM   --no-coverage - Optional. To be used if no test coverage report is needed
REM ###########################################################################

if [%1]==[] goto coverage_tests
if [%1]==[--no-coverage] goto no_coverage_tests

goto usage

:no_coverage_tests
    echo Executing Tests...
    python -m unittest discover -s edgectl\test
    if !ERRORLEVEL! NEQ 0 goto test_error
    goto test_end

:coverage_tests
    echo Executing Tests...
    coverage run -m unittest discover -s edgectl\test
    if !ERRORLEVEL! NEQ 0 goto test_error

    echo Generating Coverage Report...
    coverage report
    if !ERRORLEVEL! NEQ 0 goto test_error
    goto test_end

:usage
    echo.
    echo Usage:
    echo   %~n0%~x0
    echo   %~n0%~x0 --no-coverage
    exit /B 1

:test_error
    echo Test errors seen.
    exit /B 1

:test_end
    echo Test Done.
