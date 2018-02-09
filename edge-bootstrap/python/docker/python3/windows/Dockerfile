FROM microsoft/nanoserver

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# install embedded Python 3.6.x in the container and run tests
ENV PythonEmbedVersion=3.6.4
RUN powershell .\scripts\install_python.ps1
RUN setx /M PATH "%PATH%;C:\\ProgramData\\pyiotedge;C:\\ProgramData\\pyiotedge\\Scripts"
RUN pip install -U .[test,dev]

CMD [".\\scripts\\run_tests.bat --no-coverage"]
