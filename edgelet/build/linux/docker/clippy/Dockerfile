FROM rustlang/rust:nightly

RUN cargo +nightly install clippy

RUN set -ex; \
    apt-get update; \
    apt-get install -y \
    build-essential \
    cmake \
    uuid-dev \
    curl \
    libcurl4-openssl-dev\
    ; \
    rm -rf /var/lib/apt/lists/*

WORKDIR /volume

CMD cargo +nightly clippy --all
