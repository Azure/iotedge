ARG base_tag=1.0.6.13-linux-arm32v7
FROM azureiotedge/azureiotedge-agent-base:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
