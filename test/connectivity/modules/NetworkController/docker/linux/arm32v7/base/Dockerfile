ARG base_tag=2.1.13-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

RUN apt-get update && apt-get install -y --no-install-recommends iproute2 net-tools 