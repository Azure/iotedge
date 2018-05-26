ARG base_tag=2.1.0-rc1-runtime-alpine3.7
ARG num_procs=4

FROM alpine:3.7 as builder

# Build RocksDB for alpine
RUN apk update && \
    apk add build-base bash perl linux-headers coreutils git snappy-dev

RUN git clone -b v5.4.6 --depth 1 https://github.com/facebook/rocksdb.git && \
    cd rocksdb && \
    CFLAGS="-static-libstdc++ -Wno-psabi -DSNAPPY" PORTABLE=1 make -j ${num_procs} shared_lib && \
    strip librocksdb.so && \
    mkdir -p ../publish && \
    cp -vL librocksdb.so ../publish/

FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/app/certs
ENV SSL_CERTIFICATE_NAME=mqtt-server.pfx

# RocksDB requires snappy
RUN apk update && \
    apk add --no-cache snappy libcap

# Add an unprivileged user account for running Edge Hub
RUN adduser -Ds /bin/sh edgehubuser 
ENV EdgeHubUser=edgehubuser

# Add the CAP_NET_BIND_SERVICE capability to the dotnet binary because
# we are starting Edge Hub as a non-root user
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet

# Install RocksDB
COPY --from=builder publish/* /usr/local/lib/

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 443/tcp

CMD ["scripts/linux/start.sh"]
