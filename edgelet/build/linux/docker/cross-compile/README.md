
# Containers for Cross Compiling 

This directory has the elements to create containers to cross compile to 
various platforms, for building the edgelet packages.  Each platform here 
has corresponding scripts in [iotedge/edgelet/build/linux](../../iotedge/edgelet/build/linux) 
to assist is building the iotedge and libiothsm-std packages.

The table below lists what platforms we cross compile for and how to build these containers.


| Target                       | Arch  | Dockerfile                                           | Script |
| ---------------------------- | ----- | ---------------------------------------------------- | ------ |
| Generic ARM (Debian 9 based) | arm32 | Dockerfile.arm.armv7-unknown-linux-gnueabi           | `armbuild/build_arm_toolchain_container.sh armv7-unknown-linux-gnueabi` |
| Debian 8 (Jessie)            | arm64 | Dockerfile.debian8.aarch64-unknown-linux-gnueabi     | `debian8/build_arm64_toolchain_container.sh aarch64-unknown-linux-gnu` |
| Debian 8 (Jessie)            | arm32 | Dockerfile.debian8.armv7-unknown-linux-gnueabi       | `debian8/build_arm_toolchain_container.sh armv7-unknown-linux-gnueabi` |
| Debian 8 (Jessie)            | amd64 | Dockerfile.debian8.x86\_64-unknown-linux-gnu         | `debian8/build_amd64_container.sh` |
| Debian 9 (Stretch)           | arm32 | Dockerfile.debian9.armv7-unknown-linux-gnueabi       | `debian9/build_arm_toolchain_container.sh armv7-unknown-linux-gnueabi` |
| Debian 9 (Stretch)           | arm64 | Dockerfile.debian9.aarch64-unknown-linux-gnu         | `debian9/build_arm64_toolchain_container.sh aarch64-unknown-linux-gnu` |
| Debian 9 (Stretch)           | amd64 | Dockerfile.debian9.x86\_64-unknown-linux-gnu         | `debian9/build_amd64_container.sh` |
| Centos 7.5                   | arm64 | Dockerfile.centos7.aarch64-unknown-linux-gnueabi     | `centos/build_arm64_toolchain_container.sh aarch64-unknown-linux-gnu` |
| Centos 7.5                   | arm32 | Dockerfile.centos7.armv7-unknown-linux-gnueabi       | `centos/build_arm_toolchain_container.sh armv7-unknown-linux-gnueabi` |
| Centos 7.5                   | amd64 | Dockerfile.centos7.x86\_64-unknown-linux-gnu         | `centos/build_amd64_container.sh` |
| Ubuntu 16.04                 | arm64 | Dockerfile.ubuntu16.04.aarch64-unknown-linux-gnueabi | `ubuntu16.04/build_arm64_toolchain_container.sh aarch64-unknown-linux-gnu` |
| Ubuntu 18.04                 | arm64 | Dockerfile.ubuntu18.04.aarch64-unknown-linux-gnueabi | `ubuntu18.04/build_arm64_toolchain_container.sh aarch64-unknown-linux-gnu` |


To construct the container for the appropriate cross compile, set your 
current working directory to iotedge/edgelet/build/linux/docker/cross-compile 
(where this README is located) and execute the script in above table.


These containers only need to be rebuilt when issues arise or dependencies change.

# Adding support for new platforms

We need a container which has all of the build tools needed to compile the 
edgelet, including the libiothsm package.

This container needs:
- a compiler for the target architecture (either a cross compiler or a native compiler for the target OS).
- a toolchain for the target archicture which contains: build-essentials, cmake, curl, openssl (1.0), zlib
- QEMU, when target arch != host architecture.

The container should be named:

- for non-x86\_64 architectures: `<repository name>/<toolchain>:<OS name>_<OS version>-<container revision>`
- for x86\_64 architectures: `<repository name>/<OS name>-build:<OS version>-<container revision>`

The Docker file should be named:

`Dockerfile.<OS Name>.<target triplet>`

Inside the Dockerfile, we have scripts to assist in installing openssl, 
cmake, curl, qemu, and zlib.  The ones that are common for package managers 
are located in the `apt` and `yum` directories. The ones that are custom 
for each OS (specifically openssl) are in the `<OS name>` directory.

Write a script to build the docker image.  The script should be placed 
in directory `<OS name>`. This script should set up the appropriate toolchain 
and build the docker container.  

