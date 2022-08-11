# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine

ARG EXE_DIR=.

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy

# create a default user to allow agent to run as non-root
# value was chosen as a large value to avoid a typical regular uid, 
# and matches the "edgeagentuser" created in install packages.
ARG EDGEAGENTUSER_ID
ENV EDGEAGENTUSER_ID ${EDGEAGENTUSER_ID:-13622}
RUN adduser -Ds /bin/sh -u ${EDGEAGENTUSER_ID} edgeagentuser 

# Install RocksDB
COPY $EXE_DIR/librocksdb/librocksdb.so.armhf /usr/local/lib/librocksdb.so

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
