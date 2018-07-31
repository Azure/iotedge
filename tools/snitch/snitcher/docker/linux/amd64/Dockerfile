FROM ekidd/rust-musl-builder AS builder

ARG SRC_DIR=.

# Add source code
COPY $EXE_DIR/ ./

# Fix permissions on source code.
RUN sudo chown -R rust:rust /home/rust

# Build app
RUN cargo build --release

# Build real container

FROM alpine:latest
RUN apk --no-cache add ca-certificates
COPY --from=builder /home/rust/src/target/x86_64-unknown-linux-musl/release/snitcher \
    /usr/local/bin/

CMD /usr/local/bin/snitcher