# Network controller arm32 and arm64 require different base images. This is because arm32 needs iproute tooling and thus needs module-base-full.
ARG base_tag=1.0.5.13-linux-arm64v8
FROM azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "NetworkController.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Module" && \
    exec /usr/bin/dotnet NetworkController.dll
