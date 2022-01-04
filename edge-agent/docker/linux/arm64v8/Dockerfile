ARG base_tag=3.1.19-bionic-arm64v8

FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

RUN mkdir -p ../publish

RUN apt-get update && \
    apt-get install -y libsnappy1v5 && \
    rm -rf /var/lib/apt/lists/* && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /usr/lib/aarch64-linux-gnu/libdl.so.2 && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /lib/aarch64-linux-gnu/libdl.so && \
    ln -s /lib/aarch64-linux-gnu/libdl.so.2 /usr/lib/aarch64-linux-gnu/libdl.so && \
    ln -s /lib/aarch64-linux-gnu/libc.so.6 /usr/lib/aarch64-linux-gnu/libc.so.6

# create a user to allow agent to optionally run as non-root
ARG EDGEAGENTUSER_ID
ENV EDGEAGENTUSER_ID ${EDGEAGENTUSER_ID:-1000}
RUN useradd -ms /bin/bash -u ${EDGEAGENTUSER_ID} edgeagentuser 

COPY $EXE_DIR/librocksdb/librocksdb.so.arm64 /usr/lib/librocksdb.so
 
ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
