ARG base_tag=3.1.10-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/core/aspnet:${base_tag}

ADD ./armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd

# Add an unprivileged user account for running mqttd	
ARG MQTTDUSER_ID=1000

RUN useradd -ms /bin/bash -u ${MQTTDUSER_ID} mqttduser

EXPOSE 1883/tcp
EXPOSE 8883/tcp

USER mqttduser

ENTRYPOINT ["/usr/local/bin/mqttd"]
