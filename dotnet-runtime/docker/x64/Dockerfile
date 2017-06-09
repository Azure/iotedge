FROM debian:jessie-slim

RUN DEBIAN_FRONTEND=noninteractive apt-get update \
    && apt-get install -y --no-install-recommends \
    ########### Required Utils ########### \
        ca-certificates \
        curl \
    ########### .NET Core dependencies ########### \
        libc6 \
        libcurl3 \
        libgcc1 \
        libgssapi-krb5-2 \
        libicu52 \
        liblttng-ust0 \
        libssl1.0.0 \
        libstdc++6 \
        libunwind8 \
        libuuid1 \
        zlib1g \
    ########### Cleanup ########### \
    && echo "===> clean up..." \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Install .NET Core
ARG DOTNET_DOWNLOAD_URL=https://dotnetcli.blob.core.windows.net/dotnet/Sdk/master/dotnet-dev-ubuntu.16.04-x64.latest.tar.gz

RUN curl -SL $DOTNET_DOWNLOAD_URL --output dotnet.tar.gz \
    && mkdir -p /usr/share/dotnet \
    && tar -zxf dotnet.tar.gz -C /usr/share/dotnet \
    && rm dotnet.tar.gz \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
