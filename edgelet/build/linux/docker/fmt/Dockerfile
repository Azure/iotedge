FROM rustlang/rust:nightly

RUN rustup component add rustfmt-preview --toolchain=nightly

WORKDIR /volume

CMD cargo +nightly fmt --all -- --write-mode=diff
