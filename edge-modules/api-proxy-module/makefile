TARGET=target/release
CARGOFLAGS=--manifest-path=$(SRCDIR)/Cargo.toml
CARGO=cargo
STRIP_COMMAND=strip
SRCDIR?=.

all:
	$(CARGO) build $(CARGOFLAGS)

release:
	$(CARGO) build $(CARGOFLAGS) --release
	$(STRIP_COMMAND) $(TARGET)/api-proxy-module

clean:
	rm -rf $(TARGET)