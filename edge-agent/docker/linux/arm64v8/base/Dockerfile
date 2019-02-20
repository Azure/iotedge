ARG base_tag=3.0-runtime-bionic-arm64v8
ARG num_procs=4

FROM microsoft/dotnet:${base_tag}

RUN mkdir -p ../publish

RUN apt-get update && \
    apt-get install -y libsnappy1v5 && \
    rm -rf /var/lib/apt/lists/*

COPY  librocksdb.so /usr/lib