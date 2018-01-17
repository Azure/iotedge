@echo off
REM ###########################################################################
REM This script executes the azure-iot-edge-runtime-ctl tests
REM ###########################################################################

echo "Executing Tests..."
coverage run -m unittest discover -s edgectl\test

echo "Generating Coverage Report..."
coverage report