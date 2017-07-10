@echo off
setlocal enabledelayedexpansion

for /f "tokens=1* delims=:" %%A in ('ipconfig /all') do (
  set "line=%%~A"
  if not "!line:adapter=!"=="!line!" (
    set IPInterfaceName=!line:*adapter =!
  )
)

echo Starting Edge Service...
echo Using Interface %IPInterfaceName%
dotnet Microsoft.Azure.Devices.Edge.Service.dll

endlocal