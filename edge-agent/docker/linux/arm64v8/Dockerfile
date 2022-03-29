ARG base_tag=6.0-focal-arm64v8
ARG num_procs=4

FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

RUN mkdir -p ../publish

RUN apt-get clean && \
    apt-get update && \
    apt-get install -y libsnappy1v5 && \
    rm -rf /var/lib/apt/lists/* && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /lib/aarch64-linux-gnu/libdl.so

# create a user to allow agent to optionally run as non-root
# value was chosen as a large value to avoid a typical regular uid, 
# and matches the "edgeagentuser" created in install packages.
ARG EDGEAGENTUSER_ID
ENV EDGEAGENTUSER_ID ${EDGEAGENTUSER_ID:-13622}
RUN useradd -ms /bin/bash -u ${EDGEAGENTUSER_ID} edgeagentuser 

# COPY $EXE_DIR/librocksdb/librocksdb.so.arm64 /usr/lib/librocksdb.so
 
ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
