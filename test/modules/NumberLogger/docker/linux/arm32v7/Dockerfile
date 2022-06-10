ARG base_tag=3.1-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/runtime:${base_tag}

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

ARG EXE_DIR=.

ENV MODULE_NAME "NumberLogger.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

ENTRYPOINT /usr/bin/dotnet NumberLogger.dll

