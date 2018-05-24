SHELL=/bin/sh
TARGET=target/release
VERSION?=`cat version.txt`
REVISION?=1
PACKAGE_NAME=iotedge
PACKAGE="$(PACKAGE_NAME)-$(VERSION)"

prefix?=/usr
exec_prefix?=$(prefix)
bindir?=$(exec_prefix)/bin
libdir?=$(exec_prefix)/lib
sysconfdir?=/etc
datarootdir?=$(prefix)/share
datadir?=$(datarootdir)
mandir?=$(datarootdir)/man
man1=$(mandir)/man1
man8=$(mandir)/man8
srcdir?=.

CARGOFLAGS=--release --manifest-path=$(srcdir)/Cargo.toml
DPKGFLAGS=-b -rfakeroot -us -uc -i

INSTALL=install
INSTALL_DATA=$(INSTALL) -m 644
INSTALL_PROGRAM=$(INSTALL)
MAKE=make
MKDIR_P=mkdir -p
GZIP=gzip
SED=sed

all: $(TARGET)/iotedged $(TARGET)/iotedge

$(TARGET)/iotedged:
	cargo build $(CARGOFLAGS)

$(TARGET)/iotedge:
	cargo build $(CARGOFLAGS)

package: all
	$(INSTALL_PROGRAM) -D -s $(TARGET)/iotedged $(TARGET)/$(PACKAGE)/iotedged
	$(INSTALL_PROGRAM) -D -s $(TARGET)/iotedge $(TARGET)/$(PACKAGE)/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man1/iotedge.1 $(TARGET)/$(PACKAGE)/man/iotedge.1
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man8/iotedged.8 $(TARGET)/$(PACKAGE)/man/iotedged.8
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/config.yaml $(TARGET)/$(PACKAGE)/etc/iotedge/config.yaml

deb: package
	cp -R $(srcdir)/contrib/debian $(TARGET)/$(PACKAGE)
	$(SED) "s/@version@/${VERSION}/g; s/@revision@/${REVISION}/g;" $(srcdir)/contrib/debian/changelog > $(TARGET)/$(PACKAGE)/debian/changelog
	cd $(TARGET)/$(PACKAGE) && dpkg-buildpackage $(DPKGFLAGS)

install: all
	$(INSTALL_PROGRAM) -D -s $(TARGET)/iotedged $(DESTDIR)$(bindir)/iotedged
	$(INSTALL_PROGRAM) -D -s $(TARGET)/iotedge $(DESTDIR)$(bindir)/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man1/iotedge.1 $(DESTDIR)$(man1)/iotedge.1
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man8/iotedged.8 $(DESTDIR)$(man8)/iotedged.8
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/config.yaml $(DESTDIR)$(sysconfdir)/iotedge/config.yaml

uninstall:
	rm -f $(DESTDIR)$(bindir)/iotedged
	rm -f $(DESTDIR)$(bindir)/iotedge
	-rm -f $(DESTDIR)$(man1)/iotedge.1
	-rm -f $(DESTDIR)$(man8)/iotedged.8
	-rm $(DESTDIR)$(systconfdir)/iotedge/config.yaml

clean:
	rm -rf $(TARGET)

.PHONY: all clean deb install package uninstall
