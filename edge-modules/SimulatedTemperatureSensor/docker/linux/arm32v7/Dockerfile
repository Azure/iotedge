FROM microsoft/dotnet:2.0.0-runtime-stretch-arm32v7

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["/usr/bin/dotnet", "SimulatedTemperatureSensor.dll"]
