# syntax=docker/dockerfile:1.4

# Use the same base image as prod edgehub images
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine

ADD ./armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 

EXPOSE 1883/tcp
EXPOSE 8883/tcp

USER moduleuser

ENTRYPOINT ["/usr/local/bin/mqttd"]
