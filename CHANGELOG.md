# 1.0.4 (2018-10-31)
* Stability and reliability fixes
* AMQP+WS in Edge Hub
* Functions Binding published as Nuget package

## Edge Agent
### Features
* Allow longer createOptions fields ([ecfc2a0](https://github.com/Azure/iotedge/commit/ecfc2a0d30edced286c42d5090bd9eb953251b86))

### Bug Fixes
* N/A

## Edge Hub
### Features
* Add AMQP over Websockets protocol head ([87372c8](https://github.com/Azure/iotedge/commit/87372c88d5d6ee4c58ee2c050322d67c044f52be))
* Automatic server certificate renewal ([f557fc3](https://github.com/Azure/iotedge/commit/f557fc30a6ef5536c05accaa914d037ed8da70a7))

### Bug Fixes
* Fix updating message store endpoints when routes are updated ([98a61c0](https://github.com/Azure/iotedge/commit/98a61c0b6f81c601f8fa5edcfae79dc4f4cfbc94))
* Support C SDK CBS mode on AMQP ([84be08c](https://github.com/Azure/iotedge/commit/84be08c25bff3cc67606385422fd904e237f5580))
* Improve connection recovery after offline periods ([6069f7f](https://github.com/Azure/iotedge/commit/6069f7fb7bf292f1208d99e0890e5cd6106a6666))
* Setup storage directory in all cases ([e0a1a08](https://github.com/Azure/iotedge/commit/e0a1a081ed587351b58893e42eba9787f982aa3c))
* Fix handling of re-subscriptions after an offline period ([d8b9038](https://github.com/Azure/iotedge/commit/d8b90389b66c8c19bff012635926e90f11176fd8))

## iotedged
### Features
* Improved error messages for docker image pull failures ([0d13741](https://github.com/Azure/iotedge/commit/0d13741b1c2262a783bb60bb55d192e696278b2b), [9f500e4](https://github.com/Azure/iotedge/commit/9f500e49782314261ae9fd33c64b8cc6e4ae94f8))
* Update hyper http library to 0.12 ([10d1d79](https://github.com/Azure/iotedge/commit/10d1d79ddd953f423a955052fa0666cfda2f6c40))
* Regenerate quick start mode CA certificate on startup ([d2195f8](https://github.com/Azure/iotedge/commit/d2195f8b35f0105b9692ec2e25090890f8e53bf3))
* Add aarch64 build scripts ([13ddaa6](https://github.com/Azure/iotedge/commit/13ddaa6f66b7e8a8538c5c7155411c2a5066386d))
* Support HTTP proxy authentication ([42af84d](https://github.com/Azure/iotedge/commit/42af84d760c56a6ed297b5b88f68906808b27097))

### Bug Fixes
* Do not return container sizes in list response (performance improvement) ([8ecb27b](https://github.com/Azure/iotedge/commit/8ecb27b043a0e1fbc2293aa91cb61c65ff2d3a6a))
* Add PartOf to iotedge.socket units to enable proper shutdown ([f48a966](https://github.com/Azure/iotedge/commit/f48a9666f8a837bc19d977c8248bec147ff17d61))
* Add docker.service as a dependency of iotedged.service ([281c73e](https://github.com/Azure/iotedge/commit/281c73eb0e01172bdfd1a3e904a9aab26f950297))
* Improve Windows install/uninstall experience ([a135bdf](https://github.com/Azure/iotedge/commit/a135bdfb35fc3a163ebeaf7cb211052fa0410a16))
* Fix Stop-Service error on Windows ([466fe02](https://github.com/Azure/iotedge/commit/466fe02b2fbcd4537e9f767e8b3c0d74a032c322))

## Functions Binding
### Features
* Publish Functions Binding as a nuget package ([c7ed2b5](https://github.com/Azure/iotedge/commit/c7ed2b5f6c92ad38bd154fb13c644a5d196899b7))

### Bug Fixes
* N/A

## Temperature Sensor
### Features
* Limit number of messages sent ([d0b2196](https://github.com/Azure/iotedge/commit/d0b219631117fd078a158cbce9abb6c7cf3b031f))

### Bug Fixes
* N/A

# 1.0.3 (2018-10-09)

## Edge Agent
### Features
* Update C# SDK to 1.18.1 ([5e1a983](https://github.com/Azure/iotedge/commit/5e1a9836cd55ab3f81c6cf7c9c28018d2ca7f94b))

### Bug Fixes
* N/A

## Edge Hub
### Features
* Update C# SDK to 1.18.1 ([5e1a983](https://github.com/Azure/iotedge/commit/5e1a9836cd55ab3f81c6cf7c9c28018d2ca7f94b))
* Update Protocol Gateway to 2.0.1 ([5e1a983](https://github.com/Azure/iotedge/commit/5e1a9836cd55ab3f81c6cf7c9c28018d2ca7f94b))

### Bug Fixes
* N/A

## iotedged
### Features
* N/A

### Bug Fixes
* N/A

# 1.0.2 (2018-09-21)
* Adds HTTP Proxy support across the various components of the runtime ([956c99f](https://github.com/Azure/iotedge/commit/956c99f11eb293dc2993620aec8f106933dbe09c))

## Edge Agent
### Features
* N/A

### Bug Fixes
* Remove CamelCase property name resolver from json deserializer ([a924608](https://github.com/Azure/iotedge/commit/a924608bcf50c456e5e89108a28d8080c508b611))

## Edge Hub
### Features
* Add support for extended offline (various commits)
* Upgrade device SDK to 1.18.0 ([eeee143](https://github.com/Azure/iotedge/commit/eeee143d3250aebbbda588b631f3056bd3ab1398))
* Improve startup time ([3ac39ac](https://github.com/Azure/iotedge/commit/3ac39ac2c10b2c264c5b6c46a61263e1d0d14759))

### Bug Fixes
* Fix MQTT topic parsing for topics with a trailing slash (DeviceNotFound exception) ([2b09542](https://github.com/Azure/iotedge/commit/2b095422929ebe96885a8b0452483bf1afc0243a))
* `UpstreamProtocol` environment variable values are now case insensitive ([f48c780](https://github.com/Azure/iotedge/commit/f48c780eacfc724d5bf4752a9e6c53e9ae377c6a))
* DotNetty Timeout exceptions are mapped to general timeout exceptions ([45bac36](https://github.com/Azure/iotedge/commit/45bac36450f8821c211554b5c30e4167ec7e5e66))
* Fix potential high-bandwidth usage when SAS tokens expire ([9d2ba5e](https://github.com/Azure/iotedge/commit/9d2ba5e5d7edebf8c8e30b349754a86cb353079d))
* Fix for possible `NullReferenceException` in the `TwinManager` ([0b4ef50](https://github.com/Azure/iotedge/commit/0b4ef5073cbffd1f56d3121d8a8968bcc3517fea))
* Fix twin desired property change notification request handling ([8b1fb67](https://github.com/Azure/iotedge/commit/8b1fb6752a79591391b65c81a20e7bb65431b948))

## iotedged
### Features
* Improved error messages for missing/invalid connection strings in config.yaml ([94621d5](https://github.com/Azure/iotedge/commit/94621d524eddc162a82facaf4f5bdac01afa2bf4))

### Bug Fixes
* Fix volume creation for modules that mount volumes ([0a1a47f](https://github.com/Azure/iotedge/commit/0a1a47f3fb58a16716d58c2cd5372f0a228f7754))
* RPM changes to allow reboot ([8d29056](https://github.com/Azure/iotedge/commit/8d2905689f0d4be40006a1146ef80304efc6bb52))


## Functions Binding
### Features
* Upgrade to v2.0 of the Azure Functions runtime ([1bc69d1](https://github.com/Azure/iotedge/commit/1bc69d1e8b90406818356ab2bbb702444682b428))

### Bug Fixes
* N/A

# 1.0.1 (2018-08-21)

* Updates to license (allow redistribution) and third party notices ([9ca6055](https://github.com/azure/iotedge/commit/9ca60553735a27954b1f0345c37b39cbb18554ea))

## Edge Agent
### Features
* Update to .NET Core 2.1.2 ([542971](https://github.com/azure/iotedge/commit/54297170077285f06753dd8f590a46925e57d6de))
* Update to C# SDK 1.18.0 ([dfc72b5](https://github.com/azure/iotedge/commit/dfc72b5e41cfac066595654be59dfef301cac078))

### Bug Fixes
* Ignore version property when comparing module definitions ([2fd4bf1](https://github.com/azure/iotedge/commit/2fd4bf1d9b8e08344a9ec266cd33a9509373822c))
* Fix exception in logs when MQTT is used as upstream protocol ([2d6824b](https://github.com/azure/iotedge/commit/2d6824b90f1681801d78ba3e0b8ab47aad1dff6e))
* Reduce noise in the logs for planner failures ([29fd10e](https://github.com/azure/iotedge/commit/29fd10e61293e159bc116849e18c5a3a60c1bab9))

## Edge Hub

### Features
* Update to .NET Core 2.1.2 ([542971](https://github.com/azure/iotedge/commit/54297170077285f06753dd8f590a46925e57d6de))
* Add option to turn off protocol heads ([7a6419a](https://github.com/azure/iotedge/commit/7a6419a5474020eaa5258ac9f34930ca9930d5c6))

### Bug Fixes
* Fix backwards compatibility with iotedgectl ([cc7e142](https://github.com/azure/iotedge/commit/cc7e142ae812a4d5d2d22267052cc8602f41b5c3))
* Add `connectionDeviceId` and `connectionModuleId` properties to outgoing messages on AMQP ([e636135](https://github.com/azure/iotedge/commit/e6361358f90e344f596d2328ce0cbbae67cf7da7))
* Align direct method response with IoT Hub behavior ([539f376](https://github.com/azure/iotedge/commit/539f3760c0396f5f1d787606500cb056db1a159e))
* Prevent connecting to IoT Hub for disconnected clients. Prevents possible tight loop in token refresh ([7c77b7f](https://github.com/azure/iotedge/commit/7c77b7f2e970cd1c50ac905232f6a89fbf63317e))
* Align MQTT topic parsing with IoT Hub behavior ([b19bbb4](https://github.com/azure/iotedge/commit/b19bbb4a96c35954ebb535def8997f717b5052a4))
* Fixes receiving messages in batches over AMQP ([02f193a](https://github.com/azure/iotedge/commit/02f193a027677666c4f5c73dd515a19528554569))
* Increase twin validation limits ([2590d7e](https://github.com/azure/iotedge/commit/2590d7e87db3dd0fbd21e15cfa441dfde22f4a52))
* Align AMQP link settle modes with IoT Hub ([93f13b8](https://github.com/azure/iotedge/commit/93f13b885977c72ead1671d089e6633d4636650b))

## iotedged

### Features
* Windows installation script ([dea9cfc](https://github.com/azure/iotedge/commit/dea9cfc0c4facfdc81b6fabc49f066472817d89c))
* Support older version of systemd ([df8d10b](https://github.com/azure/iotedge/commit/df8d10b13355ae0e4f664d05723e5b10139a4ddb))
* Add RPM packages for CentOS/RHEL 7.5 ([a090acb](https://github.com/azure/iotedge/commit/a090acb8d7ba6b58e16c1ebb33c8f45698054653))

### Bug Fixes
* Fix internal server error when exec'd into a container ([31468a1](https://github.com/azure/iotedge/commit/31468a1ec03d5b2d1077064563db4b853a961eab))
* Module identity delete should return 204, not 200 ([2163103](https://github.com/azure/iotedge/commit/21631034f8baac242e321a8314d7a81e4e1ef2aa))
* Ensure modules get new server certificates when requested ([5bba698](https://github.com/azure/iotedge/commit/5bba6988569903f453159f528bc7751fdb57aa6a))

## Functions Binding

### Features
* Update to .NET Core 2.1.2 ([542971](https://github.com/azure/iotedge/commit/54297170077285f06753dd8f590a46925e57d6de))
* Update to latest Azure Functions runtime on armhf ([31ad5be](https://github.com/azure/iotedge/commit/31ad5be5eddff8917c0866509bc72d8e1c07c1f1))
* Update to C# SDK 1.18.0 ([dfc72b5](https://github.com/azure/iotedge/commit/dfc72b5e41cfac066595654be59dfef301cac078))
* Binding uses MQTT protocol by default ([f0ce4a5](https://github.com/azure/iotedge/commit/f0ce4a52139e583711fd72505327b593af605490))

## Temperature Sensor

## Features
* Update to .NET Core 2.1.2 ([542971](https://github.com/azure/iotedge/commit/54297170077285f06753dd8f590a46925e57d6de))
* Update to C# SDK 1.18.0 ([dfc72b5](https://github.com/azure/iotedge/commit/dfc72b5e41cfac066595654be59dfef301cac078))

### Bug Fixes
* Allow reset command to be an array of messages ([bf5f374](https://github.com/azure/iotedge/commit/bf5f374130931be4a0a164325147de9c171a85ca))

## iotedgectl
* Add deprecation notice

# 1.0.0 (2018-06-27)
Initial release
