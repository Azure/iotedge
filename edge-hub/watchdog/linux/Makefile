TARGET=target/release
CARGOFLAGS=--manifest-path=$(SRCDIR)/Cargo.toml
CARGO=cargo
SRCDIR?=.

all:
	$(CARGO) build $(CARGOFLAGS)

release:
	$(CARGO) build $(CARGOFLAGS) --release -p watchdog

clean:
	rm -rf $(TARGET)
