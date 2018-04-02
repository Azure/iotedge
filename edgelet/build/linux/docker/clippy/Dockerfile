FROM rustlang/rust:nightly

RUN cargo +nightly install clippy

RUN set -ex; \
    apt-get update; \
    apt-get install -y \
        build-essential \
        cmake \
        ; \
    rm -rf /var/lib/apt/lists/*

WORKDIR /volume

CMD cargo +nightly clippy --all
