FROM microsoft/dotnet:2.0.5-runtime

# Add an unprivileged user account for running Edge Hub
RUN useradd -ms /bin/bash edgehubuser
ENV EdgeHubUser=edgehubuser

ARG EXE_DIR=.
ENV SSL_CERTIFICATE_PATH=/etc/ssl/certs
ENV SSL_CERTIFICATE_NAME=mqtt-server.pfx

# Install snappy and set up symlinks that are absent from the base image
# Required by RocksDb
RUN apt-get update && \
	apt-get install -y libsnappy1v5 libcap2-bin && \
	ln -s /lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so && \
	ln -s /lib/x86_64-linux-gnu/libc.so.6 /usr/lib/x86_64-linux-gnu/libc.so

# add the CAP_NET_BIND_SERVICE capability to the dotnet binary because
# we are starting edge hub as a non-root user
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 443/tcp

CMD ["scripts/linux/start.sh"]
