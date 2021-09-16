# Configuring aziot-edged with EST

This document covers how to configure aziot-edged to issue device identity and Edge CA certificates over EST.

All of the configuration options mentioned are in `/etc/aziot/config.toml`. After editing the configuration file, run `sudo iotedge config apply` to apply the changes.

## Prerequisites

- aziot-edge and aziot-identity-service installed
- A working EST server that processes enrollment requests

## Global EST configuration

The configuration file contains a `[cert_issuance.est]` section that affects all certificates issued over EST.

```toml
[cert_issuance.est]

# Trusted root certificates to validate the EST server's TLS certificate;
# optional depending on how the EST server is configured.
# It is not required for servers with a publicly-rooted TLS certificate.
trusted_certs = [
    "file:///path/to/file.pem",
]

# Provides a default URL if the EST URL is not provided for a certificate.
# Optional if each certificate issuance specifies a URL.
[cert_issuance.est.urls]
default = "https://example.org/.well-known/est"

# Below are options for authenticating with the EST server. The required options will depend on the EST
# server's configuration. These global settings apply to all certificates that don't configure auth separately.

[cert_issuance.est.auth]
# Authentication with TLS client certificate. Provide the path of the client cert and its corresponding
# private key. These files must be readable by the users aziotcs and aziotks, respectively.
identity_cert = "file:///path/to/file.pem"
identity_pk = "file:///path/to/file.pem"

# Authentication with a TLS client certificate which will be used once to create the initial certificate.
# After the first certificate issuance, an identity_cert and identity_pk will be automatically created and
# used. Provide the path of the bootstrap client cert and its corresponding private key. These files must
# be readable by the users aziotcs and aziotks, respectively.
bootstrap_identity_cert = "file:///path/to/file.pem"
bootstrap_identity_pk = "file:///path/to/file.pem"

# Authentication with username and password.
username = "username"
password = "password"
```

The global configuration is optional if each certificate specifies its EST issuance options separately.

## Device identity certificate

The device identity certificate authenticates a device with IoT Hub during provisioning. For manual and DPS X.509-based provisioning, this certificate can be issued over EST.

### Manual X.509-based provisioning

```toml
[provisioning]
source = "manual"
iothub_hostname = "example.azure-devices.net"
device_id = "my-device"

[provisioning.authentication]
method = "x509"

[provisioning.authentication.identity_cert]
# Identifies this certificate as being issued over EST.
method = "est"

# The common name of the device identity certificate. Should match the device_id.
common_name = "my-device"

# Optional number of days between certificate issuance and expiry. Defaults to 30 if not provided.
expiry_days = 30

# Optional EST URL to issue this certificate. Defaults to the `default` URL in `[cert_issuance.est.urls]`
# if not provided. The URL must be provided either here or in default, i.e. certd will fail if no URL is
# provided here and no default exists.
url = "https://example.org/.well-known/est"

# It is also possible to configure auth separately for each certificate. The options are the
# same as in the global EST configuration and override the global configuration for their corresponding
# certificate.
identity_cert = "file:///path/to/file.pem"
identity_pk = "file:///path/to/file.pem"

bootstrap_identity_cert = "file:///path/to/file.pem"
bootstrap_identity_pk = "file:///path/to/file.pem"

username = "username"
password = "password"
```

### DPS X.509-based provisioning

```toml
[provisioning]
source = "dps"
global_endpoint = "https://global.azure-devices-provisioning.net"
id_scope = "0ab1234C5D6"

[provisioning.attestation]
method = "x509"
registration_id = "my-device"

[provisioning.authentication]
method = "x509"

[provisioning.attestation.identity_cert]
# Identifies this certificate as being issued over EST.
method = "est"

# The common name of the device identity certificate. Should match the registration_id.
common_name = "my-device"

# Optional number of days between certificate issuance and expiry. Defaults to 30 if not provided.
expiry_days = 30

# Optional EST URL to issue this certificate. Defaults to the `default` URL in `[cert_issuance.est.urls]`
# if not provided. The URL must be provided either here or in default, i.e. certd will fail if no URL is
# provided here and no default exists.
url = "https://example.org/.well-known/est"

# It is also possible to configure auth separately for each certificate. The options are the
# same as in the global EST configuration and override the global configuration for their corresponding
# certificate.
identity_cert = "file:///path/to/file.pem"
identity_pk = "file:///path/to/file.pem"

bootstrap_identity_cert = "file:///path/to/file.pem"
bootstrap_identity_pk = "file:///path/to/file.pem"

username = "username"
password = "password"
```

## Edge CA certificate

The Edge CA certificate issues identity and server certificates for Edge modules. It can be configured to be issued over EST in the `[edge_ca]` section.

```toml
[edge_ca]
method = "est"

# Optional common name of the Edge CA certificate. The Edge daemon will automatically
# generate a common name if not provided.
common_name = "my-device"

# Optional number of days between certificate issuance and expiry. Defaults to 30 if not provided.
expiry_days = 30

# Optional EST URL to issue this certificate. Defaults to the `default` URL in `[cert_issuance.est.urls]`
# if not provided. The URL must be provided either here or in default, i.e. certd will fail if no URL is
# provided here and no default exists.
url = "https://example.org/.well-known/est"

# It is also possible to configure auth separately for each certificate. The options are the
# same as in the global EST configuration and override the global configuration for their corresponding
# certificate.
identity_cert = "file:///path/to/file.pem"
identity_pk = "file:///path/to/file.pem"

bootstrap_identity_cert = "file:///path/to/file.pem"
bootstrap_identity_pk = "file:///path/to/file.pem"

username = "username"
password = "password"
```

## Sample configuration

Below is a sample configuration file that can be used as a starting point for configuring Edge with EST. It shows how to configure DPS-based provisioning, with both the device identity certificate and Edge CA certificate issued over EST.

`/etc/aziot/config.toml`:

```toml
# Configure the trusted root CA certificate in the global EST options. This section is optional
# if the EST server's TLS certificate is already trusted by the system's CA certificates.
[cert_issuance.est]
trusted_certs = ["file:///path/to/file.pem"]

# Note that the global configuration section for auth and URLs are empty because this file configures
# them separately for each certificate.

[cert_issuance.est.auth]

[cert_issuance.est.urls]

# DPS provisioning with X.509 certificate
[provisioning]
source = "dps"
global_endpoint = "https://global.azure-devices-provisioning.net"
id_scope = "0ab1234C5D6"

[provisioning.attestation]
method = "x509"
registration_id = "my-device"

# Configure the issuance of the device identity certificate.
[provisioning.attestation.identity_cert]
method = "est"
common_name = "my-device"
url = "https://est.example.com/.well-known/est/"

# The credentials to use upon initial authentication with the EST server. After the initial
# certificate enrollment, Edge will automatically create, use, and renew separate identity
# certificates.
bootstrap_identity_cert = "file:///path/to/file.pem"
bootstrap_identity_pk = "file:///path/to/file.pem"

# Configure the issuance of the Edge CA certificate.
[edge_ca]
method = "est"
url = "https://est.example.com/.well-known/est/"

# The credentials to use upon initial authentication with the EST server. After the initial
# certificate enrollment, Edge will automatically create, use, and renew separate identity
# certificates.
bootstrap_identity_cert = "file:///path/to/file.pem"
bootstrap_identity_pk = "file:///path/to/file.pem"
```

After modifying the config file, run `iotedge config apply` to apply the changes and restart Edge.

If you have run Edge previously, delete any existing certificates and keys before running `iotedge config apply` to immediately reissue the device identity and Edge CA certificates.

```sh
iotedge system stop

rm -rf /var/lib/aziot/certd/certs/*
rm -rf /var/lib/aziot/keyd/keys/*

iotedge config apply
```
