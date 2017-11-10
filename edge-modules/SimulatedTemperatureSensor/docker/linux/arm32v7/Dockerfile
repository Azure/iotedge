FROM microsoft/dotnet:2.0.0-runtime-stretch-arm32v7

ARG EXE_DIR=.

ENV MODULE_NAME "SimulatedTemperatureSensor.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

CMD ["/usr/bin/dotnet", "SimulatedTemperatureSensor.dll"]
