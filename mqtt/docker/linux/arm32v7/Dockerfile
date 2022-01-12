ARG base_tag=3.1-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

# Add an unprivileged user account for running Edge Hub
ARG EDGEHUBUSER_ID=1000
RUN useradd -ms /bin/bash -u ${EDGEHUBUSER_ID} edgehubuser
ENV EdgeHubUser=edgehubuser

ADD ./armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd

EXPOSE 1883/tcp
EXPOSE 8883/tcp

USER edgehubuser

ENTRYPOINT ["/usr/local/bin/mqttd"]