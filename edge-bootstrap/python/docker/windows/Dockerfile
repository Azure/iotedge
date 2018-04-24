FROM microsoft/powershell:6.0.1-nanoserver-1709

USER ContainerAdministrator
WORKDIR /app

# set ENV - must use setx to read and append to existing PATH
ENV PYTHONHOME="C:\\ProgramData\\pyiotedge" PYTHONPATH="C:\\ProgramData\\pyiotedge\\Lib;C:\\ProgramData\\pyiotedge\\Lib\\site-packages"
RUN setx /M PATH "%PATH%;C:\Program Files\PowerShell;C:\ProgramData\pyiotedge;C:\ProgramData\pyiotedge\Scripts"

ARG EXE_DIR=.
COPY $EXE_DIR/ ./

# install embedded Python 3.x.x in the container and run tests
ARG BASE_VERSION=3.6.4
RUN pwsh.exe -executionpolicy bypass -command .\scripts\install_python.ps1 $Env:BASE_VERSION
RUN pip install -U .[test,dev]

CMD .\scripts\run_tests.bat --no-coverage
