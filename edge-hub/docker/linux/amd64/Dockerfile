ARG base_tag=3.1-alpine

FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

ARG EXE_DIR=.	

# RocksDB requires snappy	
RUN apk update && \	
    apk add --no-cache snappy libcap	

# Add an unprivileged user account for running Edge Hub	
ARG EDGEHUBUSER_ID	
ENV EDGEHUBUSER_ID ${EDGEHUBUSER_ID:-1000}
RUN adduser -Ds /bin/sh -u ${EDGEHUBUSER_ID} edgehubuser 	

# Add the CAP_NET_BIND_SERVICE capability to the dotnet binary because	
# we are starting Edge Hub as a non-root user	
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet	

# Install RocksDB	
COPY $EXE_DIR/librocksdb/librocksdb.so.amd64 /usr/local/lib/librocksdb.so

WORKDIR /app	

COPY $EXE_DIR/ ./	

# Expose MQTT, AMQP and HTTPS ports	
EXPOSE 8883/tcp	
EXPOSE 5671/tcp	
EXPOSE 443/tcp	

USER edgehubuser	

CMD echo "$(date --utc +"%Y-%m-%d %H:%M:%S %:z") Starting Edge Hub" && \	
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Hub.Service.dll
