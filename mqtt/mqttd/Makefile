# TODO: Do we need vendor
# TODO: Do we need version
# TODO: Do we need Host uri option
# TODO: How does edgelet build release if all is the default target?

SHELL=/bin/sh
TARGET=target/release

CARGOFLAGS=--manifest-path=$(srcdir)/Cargo.toml

CARGO=cargo

srcdir?=.

all:
	$(CARGO) build $(CARGOFLAGS)

release:
	$(CARGO) build $(CARGOFLAGS) --release -p mqttd

clean:
	rm -rf $(TARGET)
