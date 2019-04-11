
# Built-in troubleshooting functionality
The built-in troubleshooting functionality in the `iotedge` CLI, "iotedge check", performs configuration and connectivity checks for commonly encountered issues.

`iotedge help check` displays detailed usage information.

# Scope

The troubleshooting tool is focused on

* Surfacing potential problems that prevent getting Edge device cloud connected.

* Surfacing potential configuration deviations from recommended production best-practices.

By design, it does not check for errors in the Edge workload deployment. Examples for these are access to any private container registry used, errors in module create options etc. The rationale being deployment validation is best performed in the facility where it is authored.

IoT Edge module logs or metrics checks are also out of scope.

# Result types

Results from checks are characterized as either **errors** or **warnings**. 

Errors have a high likelihood of preventing the Edge runtime or the modules from connecting to the cloud.

Warnings might not affect immediate connectivity but are potential deviations from best practices and may affect long term stability, offline operation or supportability of the Edge device.

# Configuration checks details

## config.yaml is well-formed

IoT Edge's config.yaml is valid and free of any syntax (e.g. whitespace) errors. Depending on the error, it may not exactly be at the line number and position reported by the parser.

## config.yaml has well-formed connection string

If a connection string is used, it is of the form: `HostName=<xyz>;DeviceId=<xyz>;SharedAccessKey=<xyz>`

## container engine is installed and functional

 IoT Edge daemon can communicate with the container engine using the path specified in `moby_runtime.uri` yaml section.

## config.yaml has correct hostname

*arsing to add*

## config.yaml has correct URIs for daemon mgmt endpoint

*arsing to add*

## latest security daemon

The installed iotedge version is checked against value from  http://aka.ms/latest-iotedged-version. The value to compare against can be overridden using the `--expected-iotedged-version` switch.

## host time is close to real time

*arsing to add*

## container time is close to host time

*arsing to add*

## DNS server (*warning*)

Checks if a DNS server is specified in the container engine's daemon.json file. The user has an option to specify this per module in the Edge device's deployment, in which case they can ignore this error. DNS best practices are documented at https://aka.ms/iotedge-prod-checklist-dns

## production readiness: certificates (*warning*)

*arsing to add*

## production readiness: certificates expiry (*warning*)

*arsing to add*

## production readiness: container engine (*warning*)

*arsing to add*

## production readiness: logs policy (*warning*)

Checks if log options and limits are specified in the container engine's daemon.json file. Specifying this for the container engine will propagate the settings to all containers managed by it. Users have an option to specify this setting, per module, in the Edge device's deployment in which case they can ignore this warning. Log management best practices are documented at https://aka.ms/iotedge-prod-checklist-logs

# Connectivity check details

## host can connect to and perform TLS handshake with IoT Hub AMQP port

A TLS connection is made and handshake performed from the host to the IoT Hub's AMQP (port 5671) endpoint. IoT Hub's FQDN is inferred from the device connection string in `config.yaml`. The IoT Hub FQDN to use can be overridden using the `--iothub-hostname` switch which should be used for performing connectivity checks for Edge devices using DPS provisioning.

By default, no IoT Edge component connects using this protocol from the host, but connectivity information in this scenario can be useful in debugging.

## host can connect to and perform TLS handshake with IoT Hub HTTPS port

A TLS connection is made and handshake performed from the host to the IoT Hub's HTTPS (port 443) endpoint. IoT Hub's FQDN is inferred from the device connection string in `config.yaml`. The IoT Hub FQDN to use can be overridden using the `--iothub-hostname` switch which should be used for performing connectivity checks for Edge devices using DPS provisioning.

## host can connect to and perform TLS handshake with IoT Hub MQTT port

A TLS connection is made and handshake performed from the host to the IoT Hub's MQTTS (port 8883) whose FQDN is inferred from the device connection string in `config.yaml`. The IoT Hub FQDN to use can be overridden using the `--iothub-hostname` switch which should be used for performing connectivity checks for Edge devices using DPS provisioning.

By default, no IoT Edge component connects using this protocol from the host, but connectivity information in this scenario can be useful in debugging.

## container on the default network can connect to IoT Hub AMQP port

A diagnostics container is launched on the default (`bridge`) container network and a connection to IoT Hub's AMQP endpoint (port 5671) is attempted from within the container. The IoT Hub's FQDN is inferred from the device connection string in `config.yaml` which can be overridden using `--iothub-hostname` switch. This test does not perform TLS handshake check because the minimal container image lacks the TLS stack.

>This check does not run for Windows containers.

## container on the default network can connect to IoT Hub HTTPS port

A diagnostics container is launched on the default (`bridge`) container network and a connection to IoT Hub's HTTPS endpoint (port 443) is attempted from within the container. The IoT Hub's FQDN is inferred from the device connection string in `config.yaml` which can be overridden using `--iothub-hostname` switch. This test does not perform TLS handshake check because the minimal container image lacks the TLS stack.


>This check does not run for Windows containers.

## container on the default network can connect to IoT Hub MQTT port

A diagnostics container is launched on the default (`bridge`) container network and a connection to IoT Hub's MQTTS endpoint (port 8883) is attempted from within the container. The IoT Hub's FQDN is inferred from the device connection string in `config.yaml` which can be overridden using `--iothub-hostname` switch. This test does not perform TLS handshake check because the minimal container image lacks the TLS stack.


>This check does not run for Windows containers.

## container on the IoT Edge module network can connect to IoT Hub AMQP port

A diagnostics container is launched on the IoT Edge container network (`azure-iot-edge` on Linux and `nat` on Windows) and a connection to IoT Hub's AMQP endpoint (port 5671) is attempted from within the container. The IoT Hub's FQDN is inferred from the device connection string in `config.yaml` which can be overridden using `--iothub-hostname` switch. This test does not perform TLS handshake check because the minimal container image lacks the TLS stack.

## container on the IoT Edge module network can connect to IoT Hub HTTPS port

A diagnostics container is launched on the IoT Edge container network (`azure-iot-edge` on Linux and `nat` on Windows) and a connection to IoT Hub's HTTPS endpoint (port 443) is attempted from within the container. The IoT Hub's FQDN is inferred from the device connection string in `config.yaml` which can be overridden using `--iothub-hostname` switch. This test does not perform TLS handshake check because the minimal container image lacks the TLS stack.

## container on the IoT Edge module network can connect to IoT Hub MQTT port

A diagnostics container is launched on the IoT Edge container network (`azure-iot-edge` on Linux and `nat` on Windows) and a connection to IoT Hub's MQTTS endpoint (port 8883) is attempted from within the container. The IoT Hub's FQDN is inferred from the device connection string in `config.yaml` which can be overridden using `--iothub-hostname` switch. This test does not perform TLS handshake check because the minimal container image lacks the TLS stack.

## Edge Hub can bind to ports on host

The default `createOptions` for Edge Hub host map ports 8883, 443 and 5671 to enable gateway use cases. If these ports are already in use on the host, it will cause the Edge Hub container to fail when starting up. This check tests the availability of these ports on the host. To get a valid result, the Edge Hub container should have attempted starting up at least once prior to running this check.

If the default port mapping is explicitly modified or removed in the Edge Hub `createOptions`, the result of this check can be ignored.