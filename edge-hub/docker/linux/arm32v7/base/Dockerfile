ARG base_tag=2.1.13-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

# Add an unprivileged user account for running Edge Hub
ARG EDGEHUBUSER_ID=1000
RUN useradd -ms /bin/bash -u ${EDGEHUBUSER_ID} edgehubuser
ENV EdgeHubUser=edgehubuser

RUN apt-get update && apt-get install -y libcap2-bin libsnappy1v5 && \
    ln -s /lib/arm-linux-gnueabihf/libdl.so.2 /usr/lib/arm-linux-gnueabihf/libdl.so && \
    ln -s /lib/arm-linux-gnueabihf/libc.so.6 /usr/lib/arm-linux-gnueabihf/libc.so && \
    rm -rf /var/lib/apt/lists/*

COPY librocksdb.so /usr/lib/

# add the CAP_NET_BIND_SERVICE capability to the dotnet binary because
# we are starting edge hub as a non-root user
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet