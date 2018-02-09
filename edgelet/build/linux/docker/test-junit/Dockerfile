FROM rustlang/rust:nightly

RUN cargo +nightly install cargo-test-junit

WORKDIR /volume

CMD cargo +nightly test-junit --name target/test-output.xml
