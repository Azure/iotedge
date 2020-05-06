ARG base_tag=2.1.13-alpine3.10
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

ARG EXE_DIR=.

RUN apk update && \
    apk add --no-cache iproute2

ENV MODULE_NAME "NetworkController.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet NetworkController.dll
