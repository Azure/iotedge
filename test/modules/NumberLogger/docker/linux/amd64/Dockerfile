ARG base_tag=3.1.12-alpine3.12
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "NumberLogger.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

ENTRYPOINT /usr/bin/dotnet NumberLogger.dll
