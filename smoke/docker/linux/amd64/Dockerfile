# Base image runs systemd, allows us to install/run iotedged in a container for testing
FROM solita/ubuntu-systemd:bionic

# Install dependencies
RUN apt-get update && \
    apt-get --yes --no-install-recommends install \
        # .NET Core dependencies, see https://github.com/dotnet/dotnet-docker/blob/d5c80ab/2.1/runtime-deps/bionic/amd64/Dockerfile
        libc6 libgcc1 libgssapi-krb5-2 libicu60 liblttng-ust0 libssl1.0.0 libstdc++6 zlib1g \
        # Needed to install iotedged
        ca-certificates \
        # Needed to get the IP address used for iotedged's management endpoint
        iproute2 && \
    rm -rf /var/lib/apt/lists/*