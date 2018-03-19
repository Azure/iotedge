FROM microsoft/dotnet:2.0.6-runtime

ARG EXE_DIR=.

ENV MODULE_NAME "TemperatureFilter.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

CMD ["/usr/bin/dotnet", "TemperatureFilter.dll"]
