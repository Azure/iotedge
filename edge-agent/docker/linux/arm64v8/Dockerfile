ARG base_tag=1.0.6.14-linux-arm64v8

FROM azureiotedge/azureiotedge-agent-base:${base_tag}
 
ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
