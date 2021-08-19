ARG base_tag=3.1.18-alpine3.13
# base dockerfile is located at \edge-util\docker\linux\amd64
FROM azureiotedge/azureiotedge-runtime-base:1.2-linux-amd64 as builder

FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}
ARG EXE_DIR=.

ENV MODULE_NAME "MetricsValidator.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet MetricsValidator.dll
