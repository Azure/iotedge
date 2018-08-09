SHELL=/bin/sh
TARGET=target/release

_version:=$(shell cat version.txt)
VERSION?=${_version}
REVISION?=1

DEB_VERSION?=$(VERSION)
DEB_REVISION?=$(REVISION)

# Converts debian versioning to rpm version
# deb 1.0.1~dev100 ~> rpm 1.0.1-0.1.dev100
RPM_VERSION?=$(word 1,$(subst ~, , $(VERSION)))
_release=$(or $(and $(word 2,$(subst ~, ,$1)),0.${REVISION}.$(word 2,$(subst ~, ,$1))),${REVISION})
RPM_RELEASE?=$(call _release, ${VERSION})

PACKAGE_NAME=iotedge
PACKAGE="$(PACKAGE_NAME)-$(RPM_VERSION)"

prefix?=/usr
exec_prefix?=$(prefix)
bindir?=$(exec_prefix)/bin
libdir?=$(exec_prefix)/lib
sysconfdir?=/etc
localstatedir?=/var
runstatedir?=$(localstatedir)/run/$(PACKAGE_NAME)
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
	VERSION=${VERSION} $(CARGO) build $(CARGOFLAGS)

release:
	VERSION=${VERSION} $(CARGO) build $(CARGOFLAGS) --release

$(TARGET)/$(PACKAGE).tar.gz:
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

dist: $(TARGET)/$(PACKAGE).tar.gz

deb: release
	$(INSTALL_PROGRAM) -D $(TARGET)/iotedged $(TARGET)/$(PACKAGE)/iotedged
	$(INSTALL_PROGRAM) -D $(TARGET)/iotedge $(TARGET)/$(PACKAGE)/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/debian/iotedge.service $(TARGET)/$(PACKAGE)/iotedge.service
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/debian/iotedge.socket $(TARGET)/$(PACKAGE)/iotedge.socket
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/debian/iotedge.mgmt.socket $(TARGET)/$(PACKAGE)/iotedge.mgmt.socket
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man1/iotedge.1 $(TARGET)/$(PACKAGE)/man/iotedge.1
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man8/iotedged.8 $(TARGET)/$(PACKAGE)/man/iotedged.8
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/linux/debian/config.yaml $(TARGET)/$(PACKAGE)/etc/iotedge/config.yaml
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/linux/logrotate $(TARGET)/$(PACKAGE)/etc/logrotate.d/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/docs/LICENSE $(TARGET)/$(PACKAGE)$(docdir)/LICENSE
	$(INSTALL_DATA) -D $(srcdir)/contrib/docs/ThirdPartyNotices $(TARGET)/$(PACKAGE)$(docdir)/ThirdPartyNotices
	$(INSTALL_DATA) -D $(srcdir)/contrib/docs/trademark $(TARGET)/$(PACKAGE)$(docdir)/trademark
	cp -R $(srcdir)/contrib/debian $(TARGET)/$(PACKAGE)
	$(SED) "s/@version@/${DEB_VERSION}/g; s/@revision@/${DEB_REVISION}/g;" $(srcdir)/contrib/debian/changelog > $(TARGET)/$(PACKAGE)/debian/changelog
	cd $(TARGET)/$(PACKAGE) && dpkg-buildpackage $(DPKGFLAGS)

rpm:
	cp $(TARGET)/$(PACKAGE).tar.gz $(rpmbuilddir)/SOURCES/
	$(SED) "s/@version@/${RPM_VERSION}/g; s/@release@/${RPM_RELEASE}/g;" $(srcdir)/contrib/centos/iotedge.spec > $(rpmbuilddir)/SPECS/iotedge.spec
	rpmbuild -v -bb --clean $(rpmbuilddir)/SPECS/iotedge.spec

install: release
	$(INSTALL_PROGRAM) -D $(TARGET)/iotedged $(DESTDIR)$(bindir)/iotedged
	$(INSTALL_PROGRAM) -D $(TARGET)/iotedge $(DESTDIR)$(bindir)/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/linux/config.yaml $(DESTDIR)$(sysconfdir)/iotedge/config.yaml
	$(INSTALL_DATA) -D $(srcdir)/contrib/config/linux/logrotate $(DESTDIR)$(sysconfdir)/logrotate.d/iotedge
	$(INSTALL_DATA) -D $(srcdir)/contrib/systemd/iotedge.service $(DESTDIR)$(unitdir)/iotedge.service
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man1/iotedge.1 $(DESTDIR)$(man1)/iotedge.1
	$(GZIP) $(DESTDIR)$(man1)/iotedge.1
	$(INSTALL_DATA) -D $(srcdir)/contrib/man/man8/iotedged.8 $(DESTDIR)$(man8)/iotedged.8
	$(GZIP) $(DESTDIR)$(man8)/iotedged.8
	$(INSTALL) -d -m 0755 $(DESTDIR)$(localstatedir)/lib/iotedge
	$(INSTALL) -d -m 0755 $(DESTDIR)$(localstatedir)/log/iotedge
	$(INSTALL) -d -m 0755 $(DESTDIR)$(runstatedir)
	$(INSTALL) -m 0660 /dev/null $(DESTDIR)$(runstatedir)/mgmt.sock
	$(INSTALL) -m 0666 /dev/null $(DESTDIR)$(runstatedir)/workload.sock
	$(INSTALL_DATA) -D $(srcdir)/contrib/docs/LICENSE $(DESTDIR)$(docdir)/LICENSE
	$(INSTALL_DATA) -D $(srcdir)/contrib/docs/ThirdPartyNotices $(DESTDIR)$(docdir)/ThirdPartyNotices
	$(INSTALL_DATA) -D $(srcdir)/contrib/docs/trademark $(DESTDIR)$(docdir)/trademark
	$(GZIP) $(DESTDIR)$(docdir)/LICENSE
	$(GZIP) $(DESTDIR)$(docdir)/ThirdPartyNotices

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

version:
	@echo "deb version:  ${DEB_VERSION}"
	@echo "deb revision: ${DEB_REVISION}"
	@echo "rpm version:  ${RPM_VERSION}"
	@echo "rpm release:  ${RPM_RELEASE}"

.PHONY: all clean deb dist install rpm uninstall version
