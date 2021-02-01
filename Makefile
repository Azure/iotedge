# Copyright (c) Microsoft. All rights reserved.

TARGET=target

_version:=$(shell cat edgelet/version.txt)
VERSION?=${_version}
REVISION?=1

# Converts debian versioning to rpm version
# deb 1.0.1~dev100 ~> rpm 1.0.1-0.1.dev100
RPM_VERSION?=$(word 1,$(subst ~, , $(VERSION)))
_release=$(or $(and $(word 2,$(subst ~, ,$1)),0.${REVISION}.$(word 2,$(subst ~, ,$1))),${REVISION})
RPM_RELEASE?=$(call _release, ${VERSION})

PACKAGE_NAME=aziot-edge
PACKAGE="$(PACKAGE_NAME)-$(RPM_VERSION)"

GIT=git
GZIP=gzip
MKDIR_P=mkdir -p

$(TARGET):
	$(MKDIR_P) $(TARGET)

$(TARGET)/$(PACKAGE).tar.gz: $(TARGET)
	@echo Running git archive...
	@$(GIT) archive --prefix=$(PACKAGE)/ -o $(TARGET)/$(PACKAGE).tar $(VERSION) 2> /dev/null || (echo 'Warning: $(VERSION) does not exist.' && $(GIT) archive --prefix=$(PACKAGE)/ -o $(TARGET)/$(PACKAGE).tar HEAD)
	@echo Running git archive submodules...
	p=`pwd` && (echo .; git submodule foreach --recursive) | while read entering path; do \
	    temp="$${path%\'}"; \
	    temp="$${temp#\'}"; \
	    path=$$temp; \
	    [ "$$path" = "" ] && continue; \
	    (cd $$path && $(GIT) archive --prefix=$(PACKAGE)/$$path/ HEAD > $$p/$(TARGET)/tmp.tar && tar --concatenate --file=$$p/$(TARGET)/$(PACKAGE).tar $$p/$(TARGET)/tmp.tar && rm $$p/$(TARGET)/tmp.tar); \
	done
	gzip -f $(TARGET)/$(PACKAGE).tar
	rm -f $(TARGET)/$(PACKAGE).tar

# Produces a tarball of the source including all git submodules
dist: $(TARGET)/$(PACKAGE).tar.gz

# Removes the build directory
clean:
	rm -rf $(TARGET)

.PHONY: clean dist version

