TARGET=target/release
CARGOFLAGS=--manifest-path=$(SRCDIR)/Cargo.toml
CARGO=cargo
STRIP_COMMAND=strip
SRCDIR?=.

all:
	$(CARGO) build $(CARGOFLAGS)

release:
	$(CARGO) build $(CARGOFLAGS) --release -p generic-mqtt-tester
	$(STRIP_COMMAND) $(TARGET)/generic-mqtt-tester

clean:
	rm -rf $(TARGET)
