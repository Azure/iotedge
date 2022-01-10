# Use the same base image as prod edgehub images
ARG base_tag=3.1-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

ADD ./armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd

EXPOSE 1883/tcp
EXPOSE 8883/tcp

# Use an unprivileged user account from base image for running mqttd	
USER edgehubuser

ENV OptimizeForPerformance false
ENTRYPOINT ["/usr/local/bin/mqttd"]