ARG base_tag=2.0.6-runtime
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "SimulatedTemperatureSensor.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

CMD ["/usr/bin/dotnet", "SimulatedTemperatureSensor.dll"]
