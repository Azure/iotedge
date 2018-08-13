# Debian Packages
Azure IoT Edge is packaged as a debian package. The following document describes
all of the actions taken during installation.

# Unpacking packages
If you would like to unpack the debian package to get at the files, you can use
the `ar` tool like so:

```
ar x iotedge_1.0.0-1.deb
```

This creates three files in the current directory:

1. `debian-binary` - regular text file containing the package version information
2. `control.tar.gz` - gzipped-tar file that contains the control directory of the package
3. `data.tar.gz` - gzipped-tar file that contains all of the files to be installed

# Packages

## iotedge
This contains the `iotedge` command line tool and `iotedged` service.

Build the package with:
```
make deb
```

### Installed Files
All of the accompanying files in the debian package can be found in the `contrib` directory in the repo: https://github.com/Azure/iotedge/tree/master/edgelet/contrib

#### Config files
[config.yaml](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/config/linux/debian/config.yaml) installed as `/etc/iotedge/config.yaml` with mode/user `400 iotedge:iotedge`

[logrotate](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/config/linux/logrotate) installed as `/etc/logrotate.d/iotedge` with mode/user `644 root:root`

#### Man Pages
[iotedge.1](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/man/man1/iotedge.1) installed as `/usr/share/man/man1/iotedge.1.gz` with mode/user `644 root:root`

[iotedged.8](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/man/man8/iotedged.8) installed as `/usr/share/man/man8/iotedge.8.gz` with mode/user `644 root:root`

#### Docs

[LICENSE](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/docs/LICENSE) installed as `/usr/share/doc/iotedge/LICENSE` with mode/user `644 root:root`

[ThirdPartyNotices](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/docs/ThirdPartyNotices) installed as `/usr/share/doc/iotedge/ThirdPartyNotices` with mode/user `644 root:root`

[trademark](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/docs/trademark) installed as `/usr/share/doc/iotedge/trademark` with mode/user `644 root:root`

#### Binaries
`iotedge` installed as `/usr/bin/iotedge` with mode/user `755 root:root`

`iotedged` installed as `/usr/bin/iotedged` with mode/user `755 root:root`

#### Systemd

[iotedge.service](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/systemd/debian/iotedge.service) installed as `/lib/systemd/system/iotedge.service` with mode/user `644 root:root`

[iotedge.socket](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/systemd/debian/iotedge.socket) installed as `/lib/systemd/system/iotedge.socket` with mode/user `644 root:root`

[iotedge.mgmt.service](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/systemd/debian/iotedge.mgmt.socket) installed as `/lib/systemd/system/iotedge.mgmt.socket` with mode/user `644 root:root`

#### SysV Init

[iotedge.init](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/debian/iotedge.init) installed as `/etc/init.d/iotedge` with mode/user `755 root:root`

[iotedge.default](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/debian/iotedge.default) installed as `/etc/default/iotedge` with mode/user `644 root:root`

### Directories

The `/var/lib/iotedge` and `/var/log/iotedge` directories are both created with mode/user `755 iotedge:iotedge`.

### Pre-install script
The pre-install script is [here](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/debian/preinst).
It creates the `iotedge` user and group, adds the `iotedge` user to the
`docker` group (so that iotedged can be run unprivileged), and adds all sudoers
to the `iotedge` group (so that the `iotedge` tool can be used without sudo).
It also verifies that a container runtime is installed before installing.

### Post-install script
The post-install script is [here](https://github.com/Azure/iotedge/blob/master/edgelet/contrib/debian/postinst).

It updates the installed config file's hostname to the device's hostname.

## libiothsm-std

Contains the hsm shared library. The source code is in [https://github.com/Azure/iotedge/tree/master/edgelet/hsm-sys/azure-iot-hsm-c](https://github.com/Azure/iotedge/tree/master/edgelet/hsm-sys/azure-iot-hsm-c).

Build the package with:

```
cmake -DBUILD_SHARED=ON -Drun_unittests=ON -Duse_emulator=OFF -DCMAKE_BUILD_TYPE=Release

make package
```

### Installed Files

#### Binaries
`libiothsm.so` installed as `/usr/lib/libiothsm.so` with mode/user `644 root:root`
