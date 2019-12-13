# IPv6 support in IoT Edge (Linux only)

IoT Edge can be configured to work on Linux devices that are on IPv6 networks. On Linux devices, IoT Edge creates modules in two different docker networks:

1. Default 'bridge' network: This network is created by docker. The Edge Agent module is deployed to this network.

2. Custom 'azure-iot-edge' network: This network is created by `iotedged`. All user defined modules along with Edge Hub are deployed to this network.

To learn more about IoT Edge networking, please refer to the [networking](./networking.md) documentation.

# Device specific configuration

Firstly, to configure docker to create container networks with IPv4/IPv6 dual-stack enabled, the following changes are required on the device:
(The following steps are based on the [Docker IPv6][1] documentation. Please refer to it for guidance)

## Configure docker for IPv4/IPv6 dual-stack support

IPv4/IPv6 dual stack support can be enabled on docker using either of the following options:

1. Edit the /etc/docker/daemon.json file to include IPv6 configuration

  ```json
  {
    "ipv6": true,
    "fixed-cidr-v6": "2021:ffff:e0:3b1:0::/80",
    "dns": ["2021:ffff:0:4:fe::6464","10.55.40.50"] // This is optional
  }
  ```

2. Use dockerd CLI

```bash
dockerd --ipv6 --fixed-cidr-v6 "2021:ffff:e0:3b1:0::/80" --dns ["2021:ffff:0:4:fe::6464","10.55.40.50"]
```

The value of fixed-cidr-v6 defines the subnet for the docker0 bridge network that gets created on the device. The Edge Agent module is deployed to the bridge network. This subnet can be obtained from your IaaS provider.

## Configure the public network interface

As per the [docker IPv6 documentation][1]:

* IPv6 forwarding may interfere with your existing IPv6 configuration. If you are using Router Advertisements to get IPv6 settings for your host’s interfaces, set accept_ra to 2. Otherwise IPv6 enabled forwarding will result in rejecting Router Advertisements. To enable router advertisements, execute the following command:

```bash
sysctl -w net.ipv6.conf.eth0.accept_ra=2
```

* If your Docker host is the only part of an IPv6 subnet but does not have an IPv6 subnet assigned, you can use NDP proxying to connect your modules to the internet via IPv6. To enable NDP proxying, execute the following command:

```bash
sysctl -w net.ipv6.conf.eth0.proxy_ndp=1
```

*The change made using the commands above don't persist after system restart, consider editing the /etc/sysctl.conf file instead to make these changes persist.*

**Please restart the docker service for the changes made above to take effect:**

```bash
systemctl restart docker
```

## Configure modules as neighbors by adding them to the neighbor proxy table

This can be achieved by either of the following two methods:

1. Adding the route of each container/module manually

    ```bash
    ip -6 neigh add proxy <Container/Module IPv6 address> dev <interface such as 'eth0'>
    ```

2. Configuring NDP Proxying daemon [ndppd][2] (Recommended)

   * To install `ndppd` run the following commands on your device:

      ```bash
      sudo apt-get update
      sudo apt-get install ndppd
      ```

   * Create /etc/ndppd.conf. You will need to define two rule sections for the eth0 network interface (or the public network interface on your device) for the two subnets:

      a. 'docker0' bridge network: This is the network with subnet defined in the daemon.json file.

      b. 'azure-iot-edge' user defined network: The subnet configuration for this network is specified in the config.yaml file of IoT Edge later.

      ```conf
      route-ttl 5000
      proxy eth0 {
        router yes
        timeout 500
        ttl 30000
        # This is the rule for the default docker 'bridge' network.
        rule 2021:ffff:e0:3b1:0::/80 {
          auto
        }
        # This is the rule for the 'azure-iot-edge' network.
        rule 2021:ffff:e0:3b1:1::/80 {
          auto
        }
      }
      ```

   * Restart the `ndppd` service

      ```bash
      systemctl restart ndppd
      ```

## Sample scripts

All the steps performed above can be automated using the [Configure docker IPv6][3] and [ndppd installation][4] sample scripts.

The [ndppd installation][4] script installs and configures the NDP proxying daemon on the device. The script takes the following parameters:

* **DOCKER0_BRIDGE_SUBNET**: The ipv6 subnet for the docker0 `bridge` network.

* **IOT_EDGE_SUBNET**: The ipv6 subnet for the `azure-iot-edge` network.

* **NETWORK_INTERFACE**: The public network interface of the device.

Sample usage:

```bash
sudo chmod +x ./installNdppd.sh
sudo ./installNdppd.sh "2021:ffff:e0:3b1:0::/80" "2021:ffff:e0:3b1:1::/80" eth0
```

The [Configure docker IPv6][3] script configures docker for IPv4/IPv6 dual-stack support, enables router advertisements and NDP proxying on the specified public network interface by editing the /etc/sysctl.conf file and
also executes the [ndppd installation][4] script. The script takes the following parameters:

* **DOCKER0_BRIDGE_SUBNET**: The ipv6 subnet for the docker0 `bridge` network.

* **IOT_EDGE_SUBNET**: The ipv6 subnet for the `azure-iot-edge` network.

* **NETWORK_INTERFACE**: The public network interface of the device.

Sample usage:

```bash
sudo chmod +x ./configureDockerIPv6.sh
sudo ./configureDockerIPv6.sh "2021:ffff:e0:3b1:0::/80" "2021:ffff:e0:3b1:1::/80" eth0
```

# IoT Edge configuration

* Specify the IPv6 network configuration for the `azure-iot-edge` network in the config.yaml file of IoT Edge. The subnet defined for this network needs to be exclusive of the subnet defined in docker's daemon.json file earlier. In other words, the subnets shouldn’t overlap. The modules in the network will pick up IP addresses from this subnet. The subnet and IP ranges specified in the configuration below should match the ones picked for the `azure-iot-edge` network while configuring the device earlier.
Sample config changes:

  ```yaml
  moby_runtime:
    uri: "unix:///var/run/docker.sock"
    network:
      name: "azure-iot-edge"
      ipv6: true
      ipam:
        config:
          - 
              gateway: '2021:ffff:e0:3b1:1::1'
              subnet: '2021:ffff:e0:3b1:1::/80'
              ip_range: '2021:ffff:e0:3b1:1::/80'
  ```

  The key changes in the config above are the specification of the `ipv6` flag with value 'true' and the IPv6 network configuration for the network itself which includes the subnet, IP range and gateway of the `azure-iot-edge` container network that will be created (Details for these can be obtained from your IaaS provider)

* Restart the docker service for the changes made above to take effect

  ```bash
  systemctl restart iotedge
  ```

IoT Edge will subsequently start up and create the `azure-iot-edge` network with IPv6 configuration as specified in the config.yaml file. Modules deployed to this network will have IPv6 addresses from within the specified subnet and IP range.

**Please note that NDP proxying needs to be set up [either manually or using ndppd](#configure-modules-as-neighbors-by-adding-them-to-the-neighbor-proxy-table) for the IoT Edge modules to have internet connectivity.**

[1]: https://docs.docker.com/v17.09/engine/userguide/networking/default_network/ipv6/
[2]: https://github.com/DanielAdolfsson/ndppd
[3]: ../scripts/linux/configureDockerIPv6.sh
[4]: ../scripts/linux/installNdppd.sh
