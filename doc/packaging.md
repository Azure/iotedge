# Packaging

## What's New

The version 1.2 of IoT Edge introduces significant changes to the underlying daemon which has been refactored and renamed (`iotedged` -> `aziot-edged`). Non-Edge specific functionality was separated into a stand-alone package, `aziot-identity-service`, on which the IoT Edge daemon now depends. The term 'service' in the name is in the context of it being a Systemd service. The `aziot-identity-service` package replaces the `libiothsm-std` package previously used. The two new packages together maintain the same design principles and goals as the original IoT Edge security daemon.

In anticipation of these large-scale changes we've made the current `iotedge` Linux package (v1.1) into a long-term servicing (LTS) release. The v1.2 of IoT Edge is now composed of two packages:

- `aziot-edge`: This package contains what remained of the security daemon (`aziot-edged`). It acts as a module runtime needed to deploy and manage containerized Edge modules. It is simply referred to as the IoT Edge daemon or the IoT Edge module runtime (MR). It has a dependency on the `aziot-identity-service` package, and on the Moby runtime package.

- `aziot-identity-service`: This package installs daemon's that are responsible for provisioning the device, managing module identities, and managing cryptographic keys and certificates. 
## Comparing Packages

A comparison of the contents of each package is below. For more in-depth discussion of the `aziot-identity-service` package, refer to its documentation on [Packaging](https://azure.github.io/iot-identity-service/packaging.html).

<table>
<thead>
<th>Item<br/><code>Package(s)</code></th>
<th>IoT Edge v1.1 (and earlier)<br/><code>iotedge</code> + <code>libiothsm-std</code></th>
<th>IoT Edge v1.2 (and later)<br/><code>aziot-edge</code></th>
<th>IoT Identity Service<br/><code>aziot-identity-service</code></th>
</thead>
<tbody>
<tr>
<td>Source repository</td>
<td><code>https://github.com/Azure/iotedge</code></td>
<td><code>https://github.com/Azure/iotedge</code></td>
<td><code>https://github.com/Azure/iot-identity-service</code></td>
</tr>
<tr>
<td>Binaries and libraries</td>
<td>

```
/usr/
├── bin/
│   ├── iotedge
│   └── iotedged
└── lib/
    └── libiothsm.so
```
</td>
<td>

```
/usr/
├── bin/
│   └── iotedge
└── libexec/
    └── aziot/
        └── aziot-edged
```

</td>
<td>

```
/usr/
├── bin/
│   └── aziotctl
├── lib/
│   └── libaziot_keys.so
└── libexec/
    └── aziot-identity-service/
        ├── aziotd
        ├── aziot-certd -> aziotd
        ├── aziot-identityd -> aziotd
        ├── aziot-keyd -> aziotd
        └── aziot-tpmd -> aziotd
```

</td>
</tr>
<tr>
<td>Command-line Interface</td>
<td>

The `iotedge` CLI tool is used to interact with the `iotedged` service. Restarting the service or viewing logs is done via `systemctl` and `journalctl` respectively.
</td>
<td>

The `iotedge` CLI tool is used to interact with the new `aziot-edged` service as well as its dependent `aziot-*` services from the `aziot-identity-service` package. Restarting or viewing logs of the combined services is done using the new `iotedge system` sub-commands. Optionally, the `systemctl` and `journalctl` tools may still be used.
</td>
<td>

The `aziotctl` CLI tool is similar to `iotedge`, but is limited in scope to the set of `aziot-*` services deployed as part of the package. It supports a subset of the commands found in the `iotedge` CLI. When IoT Edge is installed, the `iotedge` CLI should be used.

</td>
</tr>
<tr>
<td>Config files</td>
<td>

```
/etc/
└── iotedge/
    └── config.yaml
```

All configuration is done through the `config.yaml` file. The daemon must be restarted via `systemctl restart` to apply configuration changes.
</td>
<td>

```
/etc/aziot/
├── config.toml.edge.template
└── edged/
    └── config.toml.default
```

Configuration of all `aziot-*` services is done through a "super-config" file. The contents of the configuration file are applied to the individual services via the new `iotedge config apply` command. A template is installed at `/etc/aziot/config.toml.edge.template`. It includes IoT Edge-specific configuration values. Note that the expected syntax is now in TOML format. 

By default the `iotedge` CLI commands assume the "super-config" is available at `/etc/aziot/config.toml`. A common practice is to use a copy of the template as the initial `/etc/aziot/config.toml`.
</td>
<td>

```
/etc/aziot/
├── config.toml.template
├── certd/
│   └── config.toml.default
├── identityd/
│   └── config.toml.default
├── keyd/
│   └── config.toml.default
└── tpmd/
    └── config.toml.default
```

The `aziotctl config apply` command takes a configuration file in TOML format as input and generates an individual `config.toml` file for each service.

The provided `config.toml.template` is a template for configuration values that applies to the services contained in this package. It may also be used as a starting point for the `config.toml` from which configuration is applied to the individual services.
</td>
</tr>
<tr>
<td>API socket files</td>
<td>

```
/var/run/
└── iotedge/
    ├── mgmt.sock
    └── workload.sock
```
</td>
<td>

```
/run/
└── iotedge/
    ├── mgmt.sock
    └── workload.sock
```
</td>
<td>

```
/run/aziot/
├── certd.sock
├── identityd.sock
├── keyd.sock
└── tpmd.sock
```
</td>
</tr>
<tr>
<td>Systemd service and socket files</td>
<td>

```
/lib/systemd/system/
├── iotedge.service
├── iotedge.socket
└── iotedge.mgmt.socket
```
</td>
<td>

```
/lib/systemd/system/
├── aziot-edged.service
├── aziot-edged.mgmt.socket
└── aziot-edged.workload.socket
```
</td>
<td>

```
/lib/systemd/system/
├── aziot-certd.service
├── aziot-certd.socket
├── aziot-identityd.service
├── aziot-identityd.socket
├── aziot-keyd.service
├── aziot-keyd.socket
├── aziot-tpmd.service
└── aziot-tpmd.socket
```
</td>
</tr>
<tr>
<td>Unix groups (used to ACL the service sockets)</td>
<td>

`iotedge` - The `iotedge.mgmt.sock` socket

</td>
<td>

`iotedge` - The MR management socket
</td>
<td>

- `aziotcs` - The CS socket
- `aziotid` - The IS socket
- `aziotks` - The KS socket
- `aziottpm` - The TPM socket
</td>
</tr>
<tr>
<td>Home directories</td>
<td>

```
/var/lib/iotedge/
└── hsm/
    ├── certs/
    ├── cert_keys/
    └── enc_keys/
```
</td>
<td>

```
/var/lib/aziot/
└── edged/
```
</td>
<td>

```
/var/lib/aziot/
├── certd/
│   └── certs/
├── identityd/
└── keyd/
    └── keys/
```
</td>
</tr>
<tr>
<td>Package dependencies</td>
<td>

- `moby-engine`
- `openssl`
</td>
<td>

- `aziot-identity-service`
- `moby-engine`
- `openssl`
</td>
<td>

- `openssl`
</td>
</tr>
</tbody>
</table>

## Installation

For full documentation refer to the official documentation to [Install or uninstall Azure IoT Edge for Linux](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge?view=iotedge-2020-11). What follows is a brief summary with accompanying notes.

```sh
apt install aziot-edge

sudo cp /etc/aziot/config.toml.edge.template /etc/aziot/config.toml

sudo nano -w /etc/aziot/config.toml

iotedge config apply
```

After installing the `aziot-edge` package, copy the `/etc/aziot/config.toml.edge.template` to `/etc/aziot/config.toml` and edit the file to provide provisioning information.  Then run `iotedge config apply` to apply the configuration to the services. This performs initialization for both the IS+KS+CS+TPMS components installed by the `aziot-identity-service` package and the MR component installed by the `aziot-edge` package.

## Updating from `iotedge` to `aziot-edge`

```sh
apt install aziot-edge

sudo iotedge config import
```

The `iotedge config import` can be used to generate the `/etc/aziot/config.toml` based on the old `/etc/iotedge/config.yaml` left behind after uninstalling versions 1.1 or earlier of IoT Edge.

We do not support simultaneously running the services from both the old and new packages. They would step over each other trying to provision the device and manage Docker modules. We enforce mutual exclusivity between the packages by having them conflict with each other. This causes the distribution's package manager (e.g `apt` or `apt-get`) to not allow them both to be installed at the same time. This does not work if `dpkg` is used directly. Instead, the existing `iotedge` and `libiothsm-std` packages must be removed before installing the `aziot-edge` package (or even the `aziot-identity-service` package).

### Importing Configuration

This is provided as a convenience to ease the transition from v1.1 to v1.2.

```sh
iotedge config import
```

Notes on behavior:

- The device provisioning method is parsed from `/etc/iotedge/config.yaml` and translated into the provisioning information in `/etc/aziot/config.toml`.

- User-provided certificates like device ID, device CA and trust bundle, and their corresponding private key files, will be added as preloaded keys and certs in `config.toml`. The files themselves will not be moved, because they are managed by the user rather than belonging in our services' directories.

  This assumes that Microsoft's implementation of `libiothsm-std` is being used where the certs and keys are stored as files on disk. This is a reasonable assumption since we are not aware of any external Edge customers that have written their own `libiothsm-std` implementations which store keys and certs differently.

- The imported device CA will now be referred to as the "Edge CA".

- The master identity key and master encryption key are two symmetric keys dynamically generated by `iotedged` for internal use. They will not be imported into the new services.

  Note that this means Edge Agent, Edge Hub and other modules will not be able to decrypt any data when running against the new services that they previously encrypted using the workload API with the old service. This is not a problem for Edge Agent and Edge Hub, in the sense that they ignore the undecryptable data and start with empty storage, so they recover at the cost of data loss.

- The workload CA cert is no longer used with `aziot-edged` and will not be imported.

- Module identity certs and module server certs will not be imported. Modules will get new certs signed directly by the Edge CA (formerly called the device CA) when they request them.

- The `import` command does not detect and modify the access rules to the TPM. The `/etc/udev/rules.d/tpmaccess.rules` would still need to be manually updated to allow the `aziottpm` access to `tpm0` instead of `iotedge`.

### Downgrading

The old configuration from `iotedge` is not removed from its location by any of the above actions; therefore, downgrading from the new package to the old one involves uninstalling the `aziot-edge` and `aziot-identity-service` packages, then reinstalling the `iotedge` package.

```sh
sudo apt remove aziot-edge aziot-identity-service

sudo apt install iotedge
```
