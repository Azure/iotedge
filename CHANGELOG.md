# 1.0.1 (2018-08-21)

* Updates to license (allow redistribution) and third party notices (9ca60553735a27954b1f0345c37b39cbb18554ea)

## Edge Agent
### Features
* Update to .NET Core 2.1.2
* Update to C# SDK 1.18.0

### Bug Fixes
* Ignore version property when comparing module definitions (2fd4bf1d9b8e08344a9ec266cd33a9509373822c)
* Fix exception in logs when MQTT is used as upstream protocol (2d6824b90f1681801d78ba3e0b8ab47aad1dff6e)
* Reduce noise in the logs for planner failures (29fd10e61293e159bc116849e18c5a3a60c1bab9)

## Edge Hub

### Features
* Update to .NET Core 2.1.2
* Add option to turn off protocol heads (7a6419a5474020eaa5258ac9f34930ca9930d5c6)

### Bug Fixes
* Fix backwards compatibility with iotedgectl (cc7e142ae812a4d5d2d22267052cc8602f41b5c3)
* Add `connectionDeviceId` and `connectionModuleId` properties to outgoing messages on AMQP (e6361358f90e344f596d2328ce0cbbae67cf7da7)
* Align direct method response with IoT Hub behavior (539f3760c0396f5f1d787606500cb056db1a159e)
* Prevent connecting to IoT Hub for disconnected clients. Prevents possible tight loop in token refresh (7c77b7f2e970cd1c50ac905232f6a89fbf63317e)
* Align MQTT topic parsing with IoT Hub behavior (b19bbb4a96c35954ebb535def8997f717b5052a4)
* Fixes receiving messages in batches over AMQP (02f193a027677666c4f5c73dd515a19528554569)
* Increase twin validation limits (2590d7e87db3dd0fbd21e15cfa441dfde22f4a52)
* Align AMQP link settle modes with IoT Hub (93f13b885977c72ead1671d089e6633d4636650b)

## iotedged

### Features
* Windows installation script (dea9cfc0c4facfdc81b6fabc49f066472817d89c)
* Support older version of systemd (df8d10b13355ae0e4f664d05723e5b10139a4ddb)
* Add RPM packages for CentOS/RHEL 7.5 (a090acb8d7ba6b58e16c1ebb33c8f45698054653)

### Bug Fixes
* Fix internal server error when exec'd into a container (31468a1ec03d5b2d1077064563db4b853a961eab)
* Module identity delete should return 204, not 200 (21631034f8baac242e321a8314d7a81e4e1ef2aa)
* Ensure modules get new server certificates when requested (5bba6988569903f453159f528bc7751fdb57aa6a)

## Functions Binding

### Features
* Update to .NET Core 2.1.2
* Update to latest Azure Functions runtime on armhf (31ad5be5eddff8917c0866509bc72d8e1c07c1f1)
* Update to C# SDK 1.18.0
* Binding uses MQTT protocol by default (f0ce4a52139e583711fd72505327b593af605490)

## Temperature Sensor

## Features
* Update to C# SDK 1.18.0

### Bug Fixes
* Allow reset command to be an array of messages (bf5f374130931be4a0a164325147de9c171a85ca)

## iotedgectl
* Add deprecation notice

# 1.0.0 (2018-06-27)
Initial release