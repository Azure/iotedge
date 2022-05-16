# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/runtime:6.0-focal-arm64v8

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

ARG EXE_DIR=.

ENV MODULE_NAME "ModuleRestarter.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Module" && \
    exec /usr/bin/dotnet ModuleRestarter.dll
