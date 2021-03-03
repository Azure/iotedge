# Packaging

The Identity Service (IS), Keys Service (KS), Certificates Service (CS), and TPM Service (TPMS) have been designed to function as stand-alone components and can be used on Linux-based Azure IoT devices as well as Azure IoT Edge devices. To this end, we host these components in a separate source repository and ship them as a separate package. Furthermore, the existing IoT Edge daemon has been renamed (`iotedged` -> `aziot-edged`) and modified to depend on these new components for provisioning the device, managing module identities, and managing cryptographic keys and certificates. `aziot-edged`'s responsibility is to act as a Module Runtime (MR) for containerized Edge modules.

Because of these large-scale changes, we've made the current `iotedge` Linux package into a long-term servicing (LTS) release. The new components (IS/KS/CS/TPMS), along with the refactoring for an `aziot-edged` with smaller responsibilities, is shipped in two new lines of packages:

- `aziot-edge`: This package contains the MR component needed to deploy dockerized Edge modules. It will have a dependency on the `aziot-identity-service` package, and on the Moby runtime package.

- `aziot-identity-service`: This package contains the IS, KS and CS components.

A detailed comparison of the contents of the packages is below.


<table>
<thead>
<th>Item</th>
<th><code>iotedge</code> + <code>libiothsm-std</code></th>
<th><code>aziot-identity-service</code></th>
<th><code>aziot-edge</code></th>
</thead>
<tbody>
<tr>
<td>Source repository</td>
<td><code>https://github.com/Azure/iotedge</code></td>
<td><code>https://github.com/Azure/iot-identity-service</code></td>
<td><code>https://github.com/Azure/iotedge</code></td>
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

The `iotedge` CLI tool is used to interact with the `iotedged` service.
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
        ├── aziot-certd -> aziotd
        ├── aziot-identityd -> aziotd
        ├── aziot-keyd -> aziotd
        ├── aziot-tpmd -> aziotd
        └── aziotd
```

The `aziotctl` CLI tool can be used to interact with the `aziot-*` services.

(The `aziot-*` service binaries are installed under the "libexec" directory, meant for executables that are not intended to be directly invoked by a user.)
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

The `iotedge` CLI tool is used to interact with the `aziot-edged` service.
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

Note that the configuration is now in TOML format. The `aziotctl config apply` command takes a configuration file in TOML format as input and generates the Individual `config.toml` file for each service. The provided `config.toml.template` is a template for configuration values that can be set. It can be used as the super `config.toml` from which the others are generated.

</td>
<td>

```
/etc/aziot/
├── config.toml.edge.template
└── edged/
    └── config.toml
```

The `config.toml.edge.template` is a template for a super `config.toml` that additionally includes configuration values specific to IoT Edge. The role of that super `config.toml` replaces the `config.yaml` that was previously used. It will be common to use a copy of the template as the super `/etc/aziot/config.toml`. The `iotedge config apply` wraps `aziotctl config apply` in addition to generating the individual `config.toml` for the `aziot-edged` service.
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
/run/aziot/
├── certd.sock
├── identityd.sock
├── keyd.sock
└── tpmd.sock
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
<td>

```
/lib/systemd/system/
├── aziot-edged.service
├── aziot-edged.mgmt.socket
└── aziot-edged.workload.socket
```
</td>
</tr>
<tr>
<td>Unix groups (used to ACL the service sockets)</td>
<td>

`iotedge` - The `iotedge.mgmt.sock` socket

</td>
<td>

- `aziotcs` - The CS socket
- `aziotid` - The IS socket
- `aziotks` - The KS socket
- `aziottpm` - The TPM socket
</td>
<td>

`iotedge` - The MR management socket
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
├── certd/
│   └── certs/
├── identityd/
└── keyd/
    └── keys/
```
</td>
<td>

```
/var/lib/aziot/
└── edged/
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

- `openssl`
</td>
<td>

- `aziot-identity-service`
- `moby-engine`
- `openssl`
</td>
</tr>
</tbody>
</table>


## Installation procedure for IoT Edge (`aziot-edge`)

```sh
apt install aziot-edge

iotedge config
```

After installing the `aziot-edge` package, copy the `/etc/aziot/config.toml.edge.template` to `/etc/aziot/config.toml` and edit the file to provide provisioning information.  Then run `iotedge config apply` to apply the configuration to the services. This performs initialization for both the IS+KS+CS+TPMS components installed by the `aziot-identity-service` package and the MR component installed by the `aziot-edge` package.

## Updating from `iotedge` to `aziot-edge`

```sh
apt remove iotedge libiothsm-std

apt install aziot-edge

sudo iotedge config import
```

The user must remove the existing `iotedge` and `libiothsm-std` packages before installing the `aziot-edge` package (or even the `aziot-identity-service` package). We do not want a situation where the services from both packages are running at the same time. They would step over each other trying to provision the device and manage Docker modules. We enforce mutual exclusivity between the packages by having them conflict with each other so that the distribution's package manager does not allow them both to be installed at the same time.

The `iotedge config import` can be used to generate the `/etc/aziot/config.toml` based on the old `/etc/iotedge/config.yaml` left behind after uninstalling versions 1.1 or earlier of IoT Edge. 

### Configuration


```sh
iotedge init import
```

- Device provisioning method is parsed from `iotedge/config.yaml` and translated into the provisioning information in `identityd/config.toml`, `keyd/config.toml`, `certd/config.toml`, `tpmd/config.toml` and `edged/config.yaml`. For example, in case of manual-symmetric-key provisioning, the SAS key will be imported as a preloaded key in `keyd/config.toml`, and `identityd/config.toml` will be updated to use manual provisioning with a reference to the key ID.

- User-provided certificates like device ID, device CA and trust bundle, and their corresponding private key files, will be added as preloaded keys and certs in `keyd/config.toml` and `certd/config.toml`. The files themselves will not be moved, because they are managed by the user rather than belonging in our services' directories.

  This assumes that Microsoft's implementation of `libiothsm-std` is being used where the certs and keys are stored as files on disk. This is a reasonable assumption since there are no external Edge customers that have written their own `libiothsm-std` implementations which store keys and certs differently.

- The master identity key and master encryption key are two symmetric keys dynamically generated by `iotedged` for internal use. They will not me be imported into the new services.

  Note that this means Edge Agent, Edge Hub and other modules will not be able to decrypt any data when running against the new services that they previously encrypted using the workload API with the old service. This is not a problem for Edge Agent and Edge Hub, in the sense that they ignore the undecryptable data and start with empty storage, so they recover at the cost of data loss.

- Certs like workload CA and module server certs that are created dynamically by `iotedged`, and can be regenerated trivially without any problems, will not be imported into the new services.

### Downgrading

The old configuration from `iotedge` is not removed from its location by any of the above actions; therefore, downgrading from the new package to the old one simply involves uninstalling the `aziot-edge` and `aziot-identity-service` packages, then reinstalling the `iotedge` package.

```sh
sudo apt remove aziot-edge aziot-identity-service

sudo apt install iotedge
```

## Installation procedure for non-IoT Edge (`aziot-identity-service` only)

The IS+KS+CS+TPMS components can still be installed as a standalone package on devices where IoT Edge will **not** be used. They enable an application to provision a device, manage module identities, and manage cryptographic keys and certificates.

```sh
apt install aziot-identity-service

sudo cp /etc/aziot/config.toml.template /etc/aziot/config.toml

# set device provisioning information
sudo nano /etc/aziot/config.toml

aziotctl config apply
```
