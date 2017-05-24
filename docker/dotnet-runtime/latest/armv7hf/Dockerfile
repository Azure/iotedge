FROM armhf/ubuntu:16.04

RUN DEBIAN_FRONTEND=noninteractive apt-get update \
    && apt-get install -y --no-install-recommends \
       ca-certificates \
    # .NET Core dependencies \
        libunwind8 \
        libunwind8-dev \
        gettext \
        libicu-dev \
        liblttng-ust-dev \
        libcurl4-openssl-dev \
        libssl-dev \
        uuid-dev \
        curl \
    && rm -rf /var/lib/apt/lists/*

# Install .NET Core
ARG DOTNET_DOWNLOAD_URL=https://edgecommon.blob.core.windows.net/share/dotnet-linux-arm.2.0.0-preview2-25323-03.tar.gz

RUN curl -SL $DOTNET_DOWNLOAD_URL --output dotnet.tar.gz \
    && mkdir -p /usr/share/dotnet \
    && tar -zxf dotnet.tar.gz -C /usr/share/dotnet \
    && rm dotnet.tar.gz \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
