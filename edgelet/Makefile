SHELL=/bin/sh
TARGET=target/release
HSM=hsm-sys/azure-iot-hsm-c

CARGOFLAGS=--release

INSTALL=install
INSTALL_DATA=$(INSTALL) -m 644
INSTALL_PROGRAM=$(INSTALL)
MAKE=make
MKDIR_P=mkdir -p

prefix?=/usr
exec_prefix?=$(prefix)
bindir?=$(exec_prefix)/bin
libdir?=$(exec_prefix)/lib
datarootdir?=$(prefix)/share
datadir?=$(datarootdir)
mandir?=$(datarootdir)/man

all: $(TARGET)/iotedged $(TARGET)/iotedge $(TARGET)/libiothsm.so

$(TARGET)/iotedged:
	cargo build $(CARGOFLAGS)

$(TARGET)/iotedge:
	cargo build $(CARGOFLAGS)

$(HSM)/build:
	$(MKDIR_P) $(HSM)/build

$(HSM)/build/Makefile: | $(HSM)/build
	(cd $(HSM)/build; cmake -DBUILD_SHARED=ON -DCMAKE_BUILD_TYPE=Release ..)

$(TARGET)/libiothsm.so: $(HSM)/build/Makefile
	$(MAKE) -C $(HSM)/build
	cp $(HSM)/build/libiothsm.so $(TARGET)

install: all installdirs
	$(INSTALL_PROGRAM) -s $(TARGET)/iotedged $(DESTDIR)$(bindir)/iotedged
	$(INSTALL_PROGRAM) -s $(TARGET)/iotedge $(DESTDIR)$(bindir)/iotedge
	$(INSTALL_DATA) $(TARGET)/libiothsm.so $(DESTDIR)$(libdir)/libiothsm.so

installdirs: $(DESTDIR)$(bindir) $(DESTDIR)$(libdir)

uninstall:
	rm -f $(DESTDIR)$(bindir)/iotedged
	rm -f $(DESTDIR)$(bindir)/iotedge
	rm -f $(DESTDIR)$(libdir)/libiothsm.so

$(DESTDIR)$(bindir):
	$(MKDIR_P) $(DESTDIR)$(bindir)

$(DESTDIR)$(libdir):
	$(MKDIR_P) $(DESTDIR)$(libdir)

clean:
	rm -rf $(TARGET)
	rm -rf $(HSM)/build

.PHONY: all clean install installdirs uninstall
