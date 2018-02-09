FROM rustlang/rust:nightly

RUN cargo +nightly install clippy

WORKDIR /volume

CMD cargo +nightly clippy --all
