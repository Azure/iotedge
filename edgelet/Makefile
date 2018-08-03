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
docdir?=$(datarootdir)/doc/$(PACKAGE_NAME)
mandir?=$(datarootdir)/man
man1=$(mandir)/man1
man8=$(mandir)/man8
srcdir?=.

CARGOFLAGS=--manifest-path=$(srcdir)/Cargo.toml
DPKGFLAGS=-b -rfakeroot -us -uc -i

CARGO=cargo
GZIP=gzip
INSTALL=install
INSTALL_DATA=$(INSTALL) -m 644
INSTALL_PROGRAM=$(INSTALL)
MAKE=make
MKDIR_P=mkdir -p
SED=sed

all:
	$(CARGO) build $(CARGOFLAGS)

release: $(TARGET)/iotedged $(TARGET)/iotedge

$(TARGET)/iotedged:
	$(CARGO) build $(CARGOFLAGS) --release

$(TARGET)/iotedge:
	$(CARGO) build $(CARGOFLAGS) --release

prepare-deb: release
	$(INSTALL_PROGRAM) -D $(TARGET)/iotedged $(TARGET)/$(PACKAGE)/iotedged
	$(INSTALL_PROGRAM) -D $(TARGET)/iotedge $(TARGET)/$(PACKAGE)/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man1/iotedge.1 $(TARGET)/$(PACKAGE)/man/iotedge.1
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man8/iotedged.8 $(TARGET)/$(PACKAGE)/man/iotedged.8
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/linux/config.yaml $(TARGET)/$(PACKAGE)/etc/iotedge/config.yaml
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/linux/logrotate $(TARGET)/$(PACKAGE)/etc/logrotate.d/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/docs/LICENSE $(TARGET)/$(PACKAGE)$(docdir)/LICENSE
	$(INSTALL_DATA) -D $(srcdir)/contrib/docs/ThirdPartyNotices $(TARGET)/$(PACKAGE)$(docdir)/ThirdPartyNotices
	$(INSTALL_DATA) -D $(srcdir)/contrib/docs/trademark $(TARGET)/$(PACKAGE)$(docdir)/trademark

deb: prepare-deb
	cp -R $(srcdir)/contrib/debian $(TARGET)/$(PACKAGE)
	$(SED) "s/@version@/${VERSION}/g; s/@revision@/${REVISION}/g;" $(srcdir)/contrib/debian/changelog > $(TARGET)/$(PACKAGE)/debian/changelog
	cd $(TARGET)/$(PACKAGE) && dpkg-buildpackage $(DPKGFLAGS)

uninstall:
	rm -f $(DESTDIR)$(bindir)/iotedged
	rm -f $(DESTDIR)$(bindir)/iotedge
	-rm -f $(DESTDIR)$(man1)/iotedge.1
	-rm -f $(DESTDIR)$(man8)/iotedged.8
	-rm $(DESTDIR)$(systconfdir)/iotedge/config.yaml
	-rm $(DESTDIR)$(systconfdir)/logrotate.d/iotedge
	-rm $(DESTDIR)$(docdir)/LICENSE
	-rm $(DESTDIR)$(docdir)/ThirdPartyNotices
	-rm $(DESTDIR)$(docdir)/trademark

clean:
	rm -rf $(TARGET)

.PHONY: all clean deb install-deb prepare-deb uninstall
