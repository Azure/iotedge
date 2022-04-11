ARG base_tag=3.1-alpine
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "TestMetricsCollector.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet TestMetricsCollector.dll
