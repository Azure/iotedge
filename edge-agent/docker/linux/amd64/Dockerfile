ARG base_tag=3.1.19-alpine3.13
# base dockerfile is located at \edge-util\docker\linux\amd64
FROM azureiotedge/azureiotedge-runtime-base:1.2-linux-amd64 as builder

FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

ARG EXE_DIR=.

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy

# create a default user to allow agent to run as non-root
ARG EDGEAGENTUSER_ID
ENV EDGEAGENTUSER_ID ${EDGEAGENTUSER_ID:-1000}
RUN adduser -Ds /bin/sh -u ${EDGEAGENTUSER_ID} edgeagentuser 

# Install RocksDB
COPY --from=builder publish/* /usr/local/lib/

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
