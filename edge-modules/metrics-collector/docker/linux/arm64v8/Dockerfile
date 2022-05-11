ARG base_tag=3.1-bionic-arm64v8
FROM mcr.microsoft.com/dotnet/runtime:${base_tag}

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

ARG EXE_DIR=.

ENV MODULE_NAME "Microsoft.Azure.Devices.Edge.Azure.Monitor.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Module" && \
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Azure.Monitor.dll
