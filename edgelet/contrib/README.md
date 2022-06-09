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
This contains the `iotedge` command line tool and `aziot-edged` service.

Build the package with:
```
make deb
```

### Installed Files
All of the accompanying files in the debian package can be found in the `contrib` directory in the repo: https://github.com/Azure/iotedge/tree/main/edgelet/contrib

#### Config files
[config.yaml](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/config/linux/debian/config.yaml) installed as `/etc/aziot/edged/config.yaml` with mode/user `400 iotedge:iotedge`

[logrotate](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/config/linux/logrotate) installed as `/etc/logrotate.d/aziot-edge` with mode/user `644 root:root`

#### Man Pages
[iotedge.1](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/man/man1/iotedge.1) installed as `/usr/share/man/man1/iotedge.1.gz` with mode/user `644 root:root`

[aziot-edged.8](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/man/man8/aziot-edged.8) installed as `/usr/share/man/man8/aziot-edged.8.gz` with mode/user `644 root:root`

#### Docs

[LICENSE](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/docs/LICENSE) installed as `/usr/share/doc/iotedge/LICENSE` with mode/user `644 root:root`

[ThirdPartyNotices](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/docs/ThirdPartyNotices) installed as `/usr/share/doc/iotedge/ThirdPartyNotices` with mode/user `644 root:root`

[trademark](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/docs/trademark) installed as `/usr/share/doc/iotedge/trademark` with mode/user `644 root:root`

#### Binaries
`iotedge` installed as `/usr/bin/iotedge` with mode/user `755 root:root`

`aziot-edged` installed as `/usr/libexec/aziot/aziot-edged` with mode/user `755 root:root`

#### Systemd

[aziot-edged.service](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/systemd/debian/aziot-edged.service) installed as `/lib/systemd/system/aziot-edged.service` with mode/user `644 root:root`

[aziot-edged.workload.socket](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/systemd/debian/aziot-edged.workload.socket) installed as `/lib/systemd/system/aziot-edged.workload.socket` with mode/user `644 root:root`

[aziot-edged.mgmt.socket](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/systemd/debian/aziot-edged.mgmt.socket) installed as `/lib/systemd/system/aziot-edged.mgmt.socket` with mode/user `644 root:root`

#### SysV Init

[aziot-edged.init](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/debian/aziot-edged.init) installed as `/etc/init.d/aziot-edged` with mode/user `755 root:root`

[aziot-edged.default](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/debian/aziot-edged.default) installed as `/etc/default/aziot-edged` with mode/user `644 root:root`

### Directories

The `/var/lib/aziot/edged`, `/var/lib/iotedge` (only on CentOS) and `/var/log/aziot/edged` directories are created with mode/user `755 iotedge:iotedge`.

### Pre-install script
The pre-install script is [here](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/debian/preinst).
It creates the `iotedge` user and group, adds the `iotedge` user to the
`docker` group (so that aziot-edged can be run unprivileged), and adds all sudoers
to the `iotedge` group (so that the `iotedge` tool can be used without sudo).
It also verifies that a container runtime is installed before installing.

### Post-install script
The post-install script is [here](https://github.com/Azure/iotedge/blob/main/edgelet/contrib/debian/postinst).

It updates the installed config file's hostname to the device's hostname.

## libiothsm-std

Contains the hsm shared library. The source code is in [https://github.com/Azure/iotedge/tree/main/edgelet/hsm-sys/azure-iot-hsm-c](https://github.com/Azure/iotedge/tree/main/edgelet/hsm-sys/azure-iot-hsm-c).

Build the package with:

```
cmake -DBUILD_SHARED=ON -Drun_unittests=ON -Duse_emulator=OFF -DCMAKE_BUILD_TYPE=Release

make package
```

### Installed Files

#### Binaries
`libiothsm.so` installed as `/usr/lib/libiothsm.so` with mode/user `644 root:root`
