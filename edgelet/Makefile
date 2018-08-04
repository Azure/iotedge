SHELL=/bin/sh
TARGET=target/release
VERSION?=$(shell cat version.txt)
REVISION?=1
PACKAGE_NAME=iotedge
PACKAGE="$(PACKAGE_NAME)-$(VERSION)"

prefix?=/usr
exec_prefix?=$(prefix)
bindir?=$(exec_prefix)/bin
libdir?=$(exec_prefix)/lib
sysconfdir?=/etc
localstatedir?=/var
runstatedir?=$(localstatedir)/run
datarootdir?=$(prefix)/share
datadir?=$(datarootdir)
docdir?=$(datarootdir)/doc/$(PACKAGE_NAME)
mandir?=$(datarootdir)/man
man1=$(mandir)/man1
man8=$(mandir)/man8
srcdir?=.
unitdir?=/lib/systemd/system

rpmbuilddir?=$(HOME)/rpmbuild

CARGOFLAGS=--manifest-path=$(srcdir)/Cargo.toml
DPKGFLAGS=-b -rfakeroot -us -uc -i

CARGO=cargo
GIT=git
GIT_ARCHIVEFLAGS=--prefix=$(PACKAGE)/ -o $(TARGET)/$(PACKAGE).tar.gz $(GIT_TAG)
GIT_TAG=HEAD
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

dist:
	@echo Running git archive...
	@$(GIT) archive --prefix=$(PACKAGE)/ -o $(TARGET)/$(PACKAGE).tar $(VERSION) 2> /dev/null || (echo 'Warning: $(VERSION) does not exist.' && $(GIT) archive --prefix=$(PACKAGE)/ -o $(TARGET)/$(PACKAGE).tar HEAD)
	@echo Running git archive submodules...
	p=`pwd` && (echo .; cd .. && git submodule foreach --recursive) | while read entering path; do \
	    cd $$p; \
	    temp="$${path%\'}"; \
	    temp="$${temp#\'edgelet/}"; \
	    path=$$temp; \
	    [ "$$path" = "" ] && continue; \
	    (cd $$path && $(GIT) archive --prefix=$(PACKAGE)/$$path/ HEAD > $$p/$(TARGET)/tmp.tar && tar --concatenate --file=$$p/$(TARGET)/$(PACKAGE).tar $$p/$(TARGET)/tmp.tar && rm $$p/$(TARGET)/tmp.tar); \
	done
	gzip -f $(TARGET)/$(PACKAGE).tar
	rm -f $(TARGET)/$(PACKAGE).tar

prepare-deb: release
	$(INSTALL_PROGRAM) -D $(TARGET)/iotedged $(TARGET)/$(PACKAGE)/iotedged
	$(INSTALL_PROGRAM) -D $(TARGET)/iotedge $(TARGET)/$(PACKAGE)/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/iotedge.service $(TARGET)/$(PACKAGE)/iotedge.service
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/iotedge.socket $(TARGET)/$(PACKAGE)/iotedge.socket
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/iotedge.mgmt.socket $(TARGET)/$(PACKAGE)/iotedge.mgmt.socket
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

rpm: dist
	cp $(TARGET)/$(PACKAGE).tar.gz $(rpmbuilddir)/SOURCES/
	$(SED) "s/@version@/${VERSION}/g; s/@revision@/${REVISION}/g;" $(srcdir)/contrib/centos/iotedge.spec > $(rpmbuilddir)/SPECS/iotedge.spec
	rpmbuild -bb --clean $(rpmbuilddir)/SPECS/iotedge.spec

install:
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/linux/logrotate $(DESTDIR)$(sysconfdir)/logrotate.d/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/iotedge.service $(DESTDIR)$(unitdir)/iotedge.service
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/iotedge.socket $(DESTDIR)$(unitdir)/iotedge.socket
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/iotedge.mgmt.socket $(DESTDIR)$(unitdir)/iotedge.mgmt.socket
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man1/iotedge.1 $(DESTDIR)$(man1)/iotedge.1
	$(GZIP) $(DESTDIR)$(man1)/iotedge.1
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man8/iotedged.8 $(DESTDIR)$(man8)/iotedged.8
	$(GZIP) $(DESTDIR)$(man8)/iotedged.8
	$(INSTALL) -d -m 0755 $(DESTDIR)$(localstatedir)/lib/iotedge
	$(INSTALL) -d -m 0755 $(DESTDIR)$(localstatedir)/log/iotedge

#	$(INSTALL_PROGRAM) -D $(TARGET)/iotedged $(DESTDIR)$(bindir)/iotedged
#	$(INSTALL_PROGRAM) -D $(TARGET)/iotedge $(DESTDIR)$(bindir)/iotedge

install-centos7: install
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/linux/config.yaml $(DESTDIR)$(sysconfdir)/iotedge/config.yaml

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

.PHONY: all clean deb dist install-centos7 prepare-deb rpm uninstall
