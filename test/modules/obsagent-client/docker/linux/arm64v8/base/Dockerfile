FROM arm64v8/ubuntu:18.04

RUN apt-get update && apt-get install -y protoc

# Add an unprivileged user account for running the module
ARG MODULEUSER_ID=1000
RUN useradd -u ${MODULEUSER_ID} -ms /bin/bash moduleuser
USER moduleuser
