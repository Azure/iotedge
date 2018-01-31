@echo off
REM ###########################################################################
REM This script executes the azure-iot-edge-runtime-ctl tests
REM ###########################################################################

echo "Executing Tests..."
coverage run -m unittest discover -s edgectl\test
if %errorlevel% NEQ 0 goto test_error

echo "Generating Coverage Report..."
coverage report
if %errorlevel% NEQ 0 goto test_error

goto test_end

:test_error
    echo Test errors seen.
    exit /B 1

:test_end
    echo Test Done.
