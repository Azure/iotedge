ARG base_tag=3.1.19-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

RUN apt-get update && \
    apt-get install -y libsnappy1v5 && \
    ln -s /lib/arm-linux-gnueabihf/libdl.so.2 /usr/lib/arm-linux-gnueabihf/libdl.so && \
    ln -s /lib/arm-linux-gnueabihf/libc.so.6 /usr/lib/arm-linux-gnueabihf/libc.so && \
    rm -rf /var/lib/apt/lists/*

# create a user to allow agent to optionally run as non-root
# value was chosen as a large value to avoid a typical regular uid, 
# and matches the "edgeagentuser" created in install packages.
ARG EDGEAGENTUSER_ID
ENV EDGEAGENTUSER_ID ${EDGEAGENTUSER_ID:-1000}
RUN useradd -ms /bin/bash -u ${EDGEAGENTUSER_ID} edgeagentuser 

COPY $EXE_DIR/librocksdb/librocksdb.so.armhf /usr/lib/librocksdb.so

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD exec /app/agentStart.sh
