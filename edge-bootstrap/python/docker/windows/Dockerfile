FROM microsoft/powershell:6.0.1-nanoserver-1709

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

USER ContainerAdministrator
# install embedded Python 3.6.x in the container and run tests
ARG BASE_VERSION=3.6.4
ENV PythonEmbedVersion=$BASE_VERSION
RUN setx /M PATH "%PATH%;C:\\Program Files\\PowerShell"
RUN pwsh.exe -executionpolicy bypass .\scripts\install_python.ps1
RUN setx /M PATH "%PATH%;C:\\ProgramData\\pyiotedge;C:\\ProgramData\\pyiotedge\\Scripts"
RUN pip install -U .[test,dev]

CMD .\scripts\run_tests.bat --no-coverage
