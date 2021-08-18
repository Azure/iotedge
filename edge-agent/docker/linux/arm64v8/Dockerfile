ARG base_tag=1.0.5.13-linux-arm64v8

FROM azureiotedge/azureiotedge-agent-base:${base_tag}
 
ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
