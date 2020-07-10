TARGET=target/release
CARGOFLAGS=--manifest-path=$(SRCDIR)/Cargo.toml
CARGO=cargo
STRIP_COMMAND=strip
SRCDIR?=.

all:
	$(CARGO) build $(CARGOFLAGS)

release:
	$(CARGO) build $(CARGOFLAGS) --release -p mqttd
	$(STRIP_COMMAND) ../$(TARGET)/mqttd

clean:
	rm -rf ../$(TARGET)

