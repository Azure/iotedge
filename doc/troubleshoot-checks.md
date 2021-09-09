
# Built-in troubleshooting functionality
The built-in troubleshooting functionality in the `iotedge` CLI, "iotedge check", performs configuration and connectivity checks for commonly encountered issues.

`iotedge help check` displays detailed usage information.


# Scope

The troubleshooting tool is focused on

* Surfacing potential problems that prevent the edge device from connecting to the cloud/upstream.

* Surfacing potential configuration deviations from recommended production best-practices.

By design, it does not check for errors in the edge workload deployment. For example, it does not check that the device can access any private container registries, errors in module create options, etc. Deployment validation is best performed in the facility where it is authored.

Checks that would involve parsing IoT Edge module logs or metrics are also out of scope.


# Result types

Results from checks are characterized as either **errors** or **warnings**.

Errors have a high likelihood of preventing the IoT Edge runtime or the modules from connecting to the cloud/upstream.

Warnings might not affect immediate connectivity but are potential deviations from best practices, and may affect long term stability, offline operation or supportability of the edge device.

If there are warnings but no errors, the tool will exit successfully with code 0. Use `--warnings-as-errors` to treat warnings as errors.


# Configuration checks details

## config.yaml is well-formed

This check validates that IoT Edge's `config.yaml` is valid and free of any syntax (e.g. whitespace) errors.

If the check fails with an error, the line number and position reported in the error may not be the *exact* location of the problem.

## config.yaml has well-formed connection string

If the `config.yaml` uses manual provisioning with a connection string, this check validates that the connection string is well-formed and contains the required `Hostname`, `DeviceId` and `SharedAccessKey` parameters.

## container engine is installed and functional

This check validates that a container engine is installed and running, and is accessible at the endpoint specified in the `moby_runtime.uri` field.

## host OS is supported

If the device is running Windows and set to use Windows containers, this check validates that the Windows version is supported.

While the Windows installer script prevents installing on an unsupported OS version, it is possible to install on a supported OS version that then gets updated to a newer version that isn't supported.

## config.yaml has correct hostname

This check validates that the value of the `hostname` field in the `config.yaml` is the same as the device's actual hostname, or that it's a fully-qualified domain name with the device hostname as the first component.

It also validates that the value complies with RFC 1035, since some modules and downstream devices have difficulty connecting to a domain name that doesn't comply with that RFC.

If the hostname if longer than 64 characters it issues a warning. Hostname longer than 64 charaters cannot be used as local issuer in certificates.

## config.yaml has correct parent hostname

This check validates if the parent hostname exist. Parent hostname is only used when the IoT Edge device is nested.

If it exists:
It validates that the value complies with RFC 1035, since some modules and downstream devices have difficulty connecting to a domain name that doesn't comply with that RFC.
It validates that parent hostname is not longer than 64 characters.

## Resolve parent hostname inside container
When in nested configuration, this check validates that parent hostname can be resolved fom inside a container.
The extra hosts property added to edge Agent are added to the diagnostic image for name resolution.

## config.yaml has correct URIs for daemon mgmt endpoint

This check validates that the value of the `connect.management_uri` field in the `config.yaml` is valid, and that the IoT Edge daemon's management endpoint can be queried through it.

## latest security daemon

This check validates that the version of the IoT Edge daemon is the same as the value specified in <https://aka.ms/latest-iotedge-stable>

You can override the expected version using the `--expected-aziot-edged-version` switch, in which case the tool will not query that URL.

Note that the tool does *not* validate the versions of the Edge Agent and Edge Hub modules.

## host time is close to real time
This check validates that the device's local time is close to the time reported by an NTP server. `pool.ntp.org:123` is used by default, and can be overridden with the `--ntp-server` parameter.

When in nested configuration pool.ntp.org:123 might not be available and IoTedge will connect to a parent IoTedge and not to IoT Hub.
The time is the checked directly against the parent IoT edge.

## container time is close to host time

This check validates that a container sees a local time that is close to the host device's local time.

## DNS server (*warning*)

This check validates that a DNS server has been specified in the container engine's `daemon.json` file. DNS best practices are documented at <https://aka.ms/iotedge-prod-checklist-dns>

It is possible to specify a DNS server in the Edge device's deployment instead of in the container engine's `daemon.json`, and the tool does not detect this. If you have done so, you should ignore this warning.

## IPv6 network configuration

This check validates that if IPv6 container network configuration is enabled in `config.yaml` (by setting the value of `moby_runtime.network.ipv6` field to `true`), the container engine's `daemon.json` file also has IPv6 support enabled. To enable IPv6 support for the container runtime, please refer to this guide <https://aka.ms/iotedge-docker-ipv6>.

IPv6 container runtime network configuration is currently not supported for the Windows operating system and this check fails if IPv6 support is enabled in the container enginer's `daemon.json` file.

## production readiness: certificates (*warning*)

This check validates that device CA and trusted CA certificates have been defined in the `certificates` section of the `config.yaml`. If these certificates are not specified, the device operates in quickstart mode and is not supported in production. Certificate management best practices are documented at <https://aka.ms/iotedge-prod-checklist-certs>

## production readiness: certificates expiry

This check validates that the device CA certificate is valid for at least seven more days.

If the certificate has already expired, it is reported as an error. If the certificate will expire in less than seven days, it is reported as a warning.

## production readiness: container engine (*warning*)

This check validates that the container engine is the Moby container engine. Any other container engine, such as Docker CE, is not supported in production.  See <https://aka.ms/iotedge-prod-checklist-moby> for details.

## EdgeAgent module can be pulled from upstream
Try to download edge agent image using image name specified in config.yaml

## production readiness: logs policy (*warning*)

This check validates that the container engine is configured to rotate module logs, by specifying log options and limits in the container engine's `daemon.json`. Log management best practices are documented at <https://aka.ms/iotedge-prod-checklist-logs>

By setting these properties in `daemon.json`, the settings are automatically propagated to all module containers. It is also possible to specify this in the Edge device's deployment instead, and the tool does not detect this. If you have done so, you should ignore this warning.

## production readiness: Edge Agent's / Edge Hub's storage directory is persisted on the host filesystem

The tool checks the Edge Agent and Edge Hub containers to validate that their respective storage directories are mounted from the host. If this is not done, it is possible that some state is lost if the containers are deleted or updated, such as Edge Agent's cache of module state or Edge Hub's unsent messages.

These checks require the Edge Agent and Edge Hub containers to have been created.


# Connectivity check details
Note: When in nested configuration, tests try to connect to parent instead of IoThub.

## host can connect to and perform TLS handshake with DPS endpoint

If the device is set up to use DPS provisioning, the tool connects to the DPS endpoint and completes a TLS handshake with it.

## host can connect to and perform TLS handshake with IoT Hub/Upstream AMQP / HTTPS / MQTT port

The tool connects to the IoT Hub/upstream's AMQP port (5671), HTTPS port (443) and MQTT port (8883), and completes a TLS handshake for each. This verifies that the IoT Hub/upstream is reachable from the device, and that the device is configured to accept its TLS certificate.

For nested edge scenario, the FQDN of the upstream is taken from parent hostname. When using manual provisioning, the FQDN of the IoT Hub is taken from the connection string. For DPS provisioning, you must specify the FQDN of the IoT Hub using the `--iothub-hostname` parameter.

The IoT Edge daemon only uses the HTTPS protocol to connect to the IoT Hub/upstream, but connectivity from the host for the AMQP and MQTT protocols can be useful when investigating issues.

## container on the default network can connect to IoT Hub AMQP / HTTPS / MQTT port

The tool launches a diagnostics container on the default (`bridge`) container network. This container connects to the IoT Hub/upstream's AMQP port (5671), HTTPS port (443) and MQTT port (8883). This verifies that the IoT Hub is reachable from containers on the default container network.

For nested edge scenario, the FQDN of the upstream is taken from parent hostname. When using manual provisioning, the FQDN of the IoT Hub is taken from the connection string. For DPS provisioning, you must specify the FQDN of the IoT Hub using the `--iothub-hostname` parameter.

Note that these checks do not perform a TLS handshake with the IoT Hub/upstream. They only test that a TCP connection can be established to the respective port.

Note that these checks do not run for Windows containers since they are redundant with the following checks.

## container on the IoT Edge module network can connect to IoT Hub AMQP / HTTPS / MQTT port

The tool launches a diagnostics container on the IoT Edge container network specified by the `moby_runtime.network` field (defaults to `azure-iot-edge` on Linux and `nat` on Windows). This container connects to the IoT Hub/upstream's AMQP port (5671), HTTPS port (443) and MQTT port (8883). This verifies that the IoT Hub is reachable from containers on the IoT Edge container network.

For nested edge scenario, the FQDN of the upstream is taken from parent hostname. When using manual provisioning, the FQDN of the IoT Hub is taken from the connection string. For DPS provisioning, you must specify the FQDN of the IoT Hub using the `--iothub-hostname` parameter.

Note that these checks do not perform a TLS handshake with the IoT Hub. They only test that a TCP connection can be established to the respective port.

## Edge Hub can bind to ports on host

Edge Hub can bind to ports on the host so that it can be used as a gateway for leaf devices. For example, the default `createOptions` for Edge Hub set it to bind to ports 443, 5671 and 8883. If any of these ports are already in use on the host device by other services, the Edge Hub container will be unable to start up. The tool validates that Edge Hub is already running (in which case it has successfully bound to any ports it wanted to bind to), or that the ports are available for it to bind to when it does start.

On a new device, the IoT Edge daemon doesn't try to start the Edge Hub container until a deployment is applied to that device. Until then, this check will return an error because the tool can only detect which ports to test for if the IoT Edge daemon has tried to start the Edge Hub container at least once.
