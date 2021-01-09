# Use the same base image as prod edgehub images
ARG base_tag=1.0.6.4-linux-arm32v7
FROM azureiotedge/azureiotedge-hub-base:${base_tag}

ADD ./armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd

EXPOSE 1883/tcp
EXPOSE 8883/tcp

# Use an unprivileged user account from base image for running mqttd	
USER edgehubuser

ENTRYPOINT ["/usr/local/bin/mqttd"]
