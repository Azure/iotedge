TARGET=target/release
CARGOFLAGS=--manifest-path=$(SRCDIR)/Cargo.toml
CARGO=cargo
STRIP_COMMAND=strip
SRCDIR?=.

all:
	RUST_BACKTRACE=1 $(CARGO) build $(CARGOFLAGS)

release:
	RUST_BACKTRACE=1 $(CARGO) build $(CARGOFLAGS) --release -p watchdog
	$(STRIP_COMMAND) $(TARGET)/watchdog

clean:
	rm -rf $(TARGET)
