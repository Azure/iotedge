ARG base_tag=1.0.5.14-linux-arm32v7
FROM azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "DeploymentTester.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet DeploymentTester.dll
