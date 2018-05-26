ARG base_tag=2.1.0-rc1-runtime-alpine3.7
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "TemperatureFilter.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Edge Agent" && \
    exec /usr/bin/dotnet TemperatureFilter.dll
