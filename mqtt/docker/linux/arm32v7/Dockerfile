# Use the same base image as prod edgehub images
ARG base_tag=3.1-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

# create a user to allow agent to optionally run as non-root
ARG EDGEUSER_ID
ENV EDGEUSER_ID ${EDGEUSER_ID:-1000}
RUN useradd -ms /bin/bash -u ${EDGEUSER_ID} edgeuser 

#Nonsense

ADD ./armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd

EXPOSE 1883/tcp
EXPOSE 8883/tcp

ENTRYPOINT ["/usr/local/bin/mqttd"]