FROM microsoft/dotnet:2.0.0-runtime

ARG EXE_DIR=.

ENV MODULE_NAME "SimulatedTemperatureSensor.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["scripts/linux/start.sh"]
