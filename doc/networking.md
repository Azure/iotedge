# Networking on IoT Edge

IoT Edge uses the networking capabilities of the Moby runtime to connect to IoT Hub and provide connectivity between modules.
In a basic IoT Edge setup, the default configuration should be sufficient.
However, if there are additional requirements for firewalls or network topology, it is helpful to understand IoT Edge's network setup and dependencies.

On Windows, all containers are started on the Moby [nat network][3]. The only requirement for IoT Edge is that this nat network has outbound internet connectivity to IoT Hub, a container registry, and optionally the Device Provisioning Service.

# Default Topology

![IoT Edge network][network]

# Networks

By default, IoT Edge uses two networks for connectivity between containers.
This allows an additional form of isolation between the host network, the Edge Agent, and application containers.

The Edge Agent attaches to the [default docker network][1] (name and type are `bridge` on Linux, `nat` on Windows).
Inside a Linux container, this default network device shows up as `docker0` when running `ifconfig`.

On Linux, the remaining containers, including the Edge Hub, are placed on a [user-defined network][2] named `azure-iot-edge` . Like the default network, this network's type is `bridge` on Linux, `nat` on Windows.

## Default Network

The Edge Agent is started by `iotedged` and is started without any explicit networking configuration.
By default, the Moby runtime places all containers without a defined network configuration on the default network (named `bridge` on Linux, `nat` on Windows).

The Edge Agent requires outbound internet connectivity to IoT Hub to function properly.
This means that a route from the default docker subnet to the internet must exist, no firewall rules are setup to block traffic, and IP forwarding is enabled.
All three of these conditions are met in the standard Moby installation.

### Configuring the Default Network

The Moby Engine creates and configures the default network.
Changing this configuration is done at the Moby Engine level, by modifying the `daemon.json` config file and restarting the Moby Engine.
This file is normally located at `/etc/docker/daemon.json` on Linux.

Reasons for changing this configuration include:
* Modifying the IP block used for assigning IPs to containers
* Changing the MTU
* Changing the gateway address

The usual reason for modifying this configuration is that some other subnet on the network clashes with the default docker subnet.

## User-defined Network (Linux only)

On Linux, all modules (containers), including the Edge Hub, are started by the Edge Agent and placed on a user-defined network named `azure-iot-edge`.
This network is created when the `iotedged` boots for the first time.

The Edge Hub requires outbound internet connectivity to IoT Hub to function properly.
This means that a route from the `azure-iot-edge` subnet to the internet must exist and no firewall rules are set up to block traffic.

### Configuring `azure-iot-edge`

This network is configured via the `moby_runtime` section of the `iotedged`'s config file (`/etc/iotedge/config.yaml):

```yaml
moby_runtime:
  uri: "unix:///var/run/docker.sock"
  network: "azure-iot-edge"
```

Additional container network configuration such as enabling IPv6 networking and providing the IPAM settings can be achieved by specifying the relevant configuration in the network settings.

```yaml
moby_runtime:
  uri: "unix:///var/run/docker.sock"
  network:
    name: "azure-iot-edge"
    ipv6: true
    ipam:
      - 
          gateway: '172.18.0.1'
          subnet: '172.18.0.0/16'
          ip_range: '172.18.0.0/16'
      - 
          gateway: '2021:ffff:e0:3b1:1::1'
          subnet: '2021:ffff:e0:3b1:1::/80'
          ip_range: '2021:ffff:e0:3b1:1::/80'
```

Any changes to other specific settings of this network must be made out of band, via the Moby Engine.
Read the Docker [networking guide][4] for more information.

This network can be configured before starting IoT Edge, as the `iotedged` does a "get or create" operation on the network when starting.
Pre-configuring a network and updating the network in the `config.yaml` allows complete control over its settings.

# Ports

IoT Edge does not require any inbound ports to be open for proper operation.

Depending on the scenario, IoT Edge does require several ports to be open for outbound connectivity.
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
The Moby Engine requires outbound connectivity on port 443 to a container registry.

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
