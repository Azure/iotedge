# Use the same base image as prod edgehub images
FROM mcr.microsoft.com/dotnet/aspnet:6.0-focal-arm32v7

ADD ./armv7-unknown-linux-gnueabihf/release/mqttd /usr/local/bin/mqttd

EXPOSE 1883/tcp
EXPOSE 8883/tcp

ENTRYPOINT ["/usr/local/bin/mqttd"]
