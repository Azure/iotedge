ARG base_tag=3.1.19-alpine3.13
FROM mcr.microsoft.com/dotnet/runtime:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "MetricsCollector.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet MetricsCollector.dll
