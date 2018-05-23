ARG base_tag=2.1.0-rc1-runtime-nanoserver-1709
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo %DATE:~4,2%-%DATE:~7,2%-%DATE:~10,4% %TIME%: Starting Module && \
    dotnet SimulatedTemperatureSensor.dll
