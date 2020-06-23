TARGET=target/release
CARGOFLAGS=--manifest-path=$(SRCDIR)/Cargo.toml
CARGO=cargo
SRCDIR?=.

# TODO: should we have a make target that doesn't build release?
all:
	$(CARGO) build $(CARGOFLAGS)

release:
	$(CARGO) build $(CARGOFLAGS) --release -p watchdog

clean:
	rm -rf $(TARGET)
