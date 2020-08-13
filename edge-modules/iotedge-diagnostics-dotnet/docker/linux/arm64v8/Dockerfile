ARG base_tag=1.0.6.0-linux-arm64v8
FROM azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Module is intended to be used with docker run command"

