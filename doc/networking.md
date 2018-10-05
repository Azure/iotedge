# Networking on IoT Edge

IoT Edge uses the networking capabilities of the Moby runtime to connect to IoT Hub and provide connectivity between modules.
In a basic IoT Edge setup, the default configuration should be sufficient.
However, if there are additional requirements for firewalls or network topology, it is helpful to understand IoT Edge's network setup and dependencies.

On Windows, all containers are started on the Moby [nat network][3]. The only requirement for IoT Edge is that this nat network has outbound internet connectivity to IoT Hub, a container registry, and optionally the Device Provisioning Service.

The remainder of this document discusses the setup on Linux, which provides better isolation, but is more complex.

# Default Topology

![IoT Edge network][network]

# Bridge Networks

By default, IoT Edge uses two bridge networks for connectivity between containers.
This allows an additional form of isolation between the host network, the Edge Agent, and application containers.

The Edge Agent attaches to the [default docker bridge network][1] (named `bridge`).
This bridge network device shows up as `docker0` when running `ifconfig`.

The remaining containers, including the Edge Hub, are placed on a [user-defined bridge network][2] (named `azure-iot-edge`).

## Default Bridge Network (docker0)

The Edge Agent is started by `iotedged` and is started without any explicit networking configuration.
By default, the Moby runtime places all containers without a defined network configuration on the default bridge network (`bridge`).

The Edge Agent requires outbound internet connectivity to IoT Hub to function properly.
This means that a route from the default docker bridge subnet to the internet must exist, no firewall rules are setup to block traffic, and IP forwarding is enabled.
All three of these conditions are met in a standard Docker installation.

### Configuring `bridge`

The Docker Engine creates and configures the default bridge network.
Changing this configuration is done at the Docker Engine level, by modifying the `daemon.json` config file and restarting the Docker Engine.
This file is normally located at `/etc/docker/daemon.json` on Linux.

Reasons for changing this configuration include:
* Modifying the IP block used for assigning IPs to containers
* Changing the MTU
* Changing the gateway address

The usual reason for modifying this configuration is that some other subnet on the network clashes with the bridge network subnet.

## `azure-iot-edge` Network

Modules (containers), including the Edge Hub, are started by the Edge Agent and placed on a user-defined bridge network named `azure-iot-edge`.
This network is created when the `iotedged` boots for the first time.

The Edge Hub requires outbound internet connectivity to IoT Hub to function properly.
This means that a route from the bridge subnet to the internet must exist and no firewall rules are setup to block traffic.

### Configuring `azure-iot-edge`

This network name is configured via the `moby_runtime` section of the `iotedged`'s config file (`/etc/iotedge/config.yaml):

```yaml
moby_runtime:
  uri: "unix:///var/run/docker.sock"
  network: "azure-iot-edge"
```

Any changes to the specific settings of this network must be made out of band, via the Docker Engine.
Read the Docker [networking guide][4] for more information.

This network can be configured before starting IoT Edge, as the `iotedged` does a "get or create" operation on the network when booting.
Pre-configuring a network and updating the network in the `config.yaml` allows complete control over its settings.

# Ports

IoT Edge does not require any inbound ports to be open for proper operation.

IoT Edge does require several ports to be open for outbound connectivity.
The following table describes these requirements:

|Protocol | Port | Inbound                         | Outbound  |
|---------|------|---------------------------------|-----------|
| MQTT    | 8883 | OPTIONAL (For gateway scenario) | OPEN*     | 
| MQTT+WS | 443  | OPTIONAL (For gateway scenario) | OPEN*     |
| AMQP    | 5671 | OPTIONAL (For gateway scenario) | OPEN*     | 
| AMQP+WS | 443  | OPTIONAL (For gateway scenario) | OPEN*     |
| HTTPS   | 443  | OPTIONAL (For gateway scenario) | OPEN      |

In the gateway scenario, at least one of the Edge Hub's supported protocols must be open for downstream devices to connect.
This means that one of 8883, 5671, and 443 must be open to inbound access.
If no downstream devices are to connect to the edge device as a gateway, then all inbound connectivity can be disabled.

*The `Edge Agent` and `Edge Hub` require one of ports 5671, 8883, or 443 open for outbound connectivity to IoT Hub.
The `iotedged` requires outbound connectivity on port 443 to IoT Hub, and optionally to the Device Provisioning Service (DPS), if DPS is used for provisioning.
The Docker Engine requires outbound connectivity on port 443 to a container registry.

# Upstream Protocol

The protocol (and thus the port used) for the upstream communication to IoT Hub can be configured for the `Edge Agent` and `Edge Hub` by setting the `UpstreamProtocol` environment variable on both the `Edge Agent` and `Edge Hub` via a deployment.

The valid values are:
* `Mqtt`
* `Amqp`
* `MqttWs`
* `AmqpWs`


[1]: https://docs.docker.com/network/bridge/#use-the-default-bridge-network
[2]: https://docs.docker.com/network/bridge/
[3]: https://docs.microsoft.com/en-us/virtualization/windowscontainers/container-networking/network-drivers-topologies
[4]: https://docs.docker.com/network/

[network]: images/iotedge-network.png
