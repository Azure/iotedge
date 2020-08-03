ARG base_tag=1.0.6.1-linux-arm64v8
FROM azureiotedge/azureiotedge-module-base-full:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "TestResultCoordinator.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose HTTP port
EXPOSE 5001/tcp

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Module" && \
    exec /usr/bin/dotnet TestResultCoordinator.dll
