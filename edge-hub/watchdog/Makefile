TARGET=target/release
CARGOFLAGS=--manifest-path=$(SRCDIR)/Cargo.toml
CARGO=cargo
STRIP_COMMAND=strip
SRCDIR?=.

all:
	$(CARGO) build $(CARGOFLAGS)

release:
	$(CARGO) build $(CARGOFLAGS) --release -p watchdog
	$(STRIP_COMMAND) $(TARGET)/watchdog

clean:
	rm -rf $(TARGET)
