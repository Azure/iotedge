# 1.0.8 (2019-07-22)
* Preview support for Linux arm64
* Upgrade Moby version in .cab file to 3.0.5 ([f23aca1](https://github.com/Azure/iotedge/commit/f23aca1fb532574e6ee7ebb0b70452d4c672ae1a))
* Update .NET Core version to 2.1.10 ([ad345ef](https://github.com/Azure/iotedge/commit/ad345efae692bbf3e28dc3d763f32ab25d667265))
* Stability improvements
* Upgrade C# Client SDK to 1.20.3 and Service SDK to 1.18.1
* Various improvements to `iotedge check` troubleshooting command
* Fix Win install setup for symmetric key provisioning mode ([602472f](https://github.com/Azure/iotedge/commit/602472fa2a205e08cf87b345544a364eea09a5dd))

## Edge Agent
### Features
* Support for arm64 ([6189e21](https://github.com/Azure/iotedge/commit/6189e21c47c474ce719685b504d1e2bcde1304f2))
* Initial support for remote get of module logs ([c49f957](https://github.com/Azure/iotedge/commit/c49f957c67ab8362b7e939bc348ed7e853c2c154), [6bc92d2](https://github.com/Azure/iotedge/commit/6bc92d2e235cbdbb24d81d7931253e7c3d81b8eb), [e064a59](https://github.com/Azure/iotedge/commit/e064a599a4842b58b6ff6bd4e88b5b7a1711a828), [5b310b1](https://github.com/Azure/iotedge/commit/5b310b137381d736fec3400909d3a4d36d18994c), [a8cdf8d](https://github.com/Azure/iotedge/commit/a8cdf8daf25fe6d36933494b51272eb425c9d9c6), [75d7460](https://github.com/Azure/iotedge/commit/75d74603664d1f206585ab3473294236b142a011), [951afd8](https://github.com/Azure/iotedge/commit/951afd8cad725bb5fa9b2d4b4ede3f2e047d21e3), [edaad81](https://github.com/Azure/iotedge/commit/edaad8191b854f52ad3d72ba92dc63c22fff685e), [83118b2](https://github.com/Azure/iotedge/commit/83118b2d7ecf8c65be8c07f682294d4fad01b0b3), [5ce1903](https://github.com/Azure/iotedge/commit/5ce1903c54f40d2646f236b7e3fe1e96f278100d), [372026e](https://github.com/Azure/iotedge/commit/372026eb2b9b6df5547dbadc28e182fbd29d26df))
* Additional optional settings to limit upstream bandwidth usage

### Bug Fixes
* Fix NRE in IotHubReporter.ReportShutdown ([81065db](https://github.com/Azure/iotedge/commit/81065db19033c0a4c6aac634b69f837581f8c466))
* In some cases Edge Agent won't restart a stopped module ([6261fc9](https://github.com/Azure/iotedge/commit/6261fc9dc69773332f742295f35d71ac0d4aa35c))
* Edge Agent can support local Docker registries ([2086d4b](https://github.com/Azure/iotedge/commit/2086d4bf40cdce74ffd3f5cc906ee576e7dc848f))
* Be more resilient on GetTwin calls ([2c4bc2a](https://github.com/Azure/iotedge/commit/2c4bc2aa54827b5e1500fb4e48c88e31d78fc833))
* Strip headers in get logs calls when sending to blob store ([95a657a](https://github.com/Azure/iotedge/commit/95a657af429ab7d444af035f01d9bbbae4d09b8d))
* Implement equality on registry credentials to prevent unnecessary backup ([c6b0ba9](https://github.com/Azure/iotedge/commit/c6b0ba9eff39a64e05fd5de41406a794b0bdfc95))
* Add timeout to workload client calls ([a1b77bf](https://github.com/Azure/iotedge/commit/a1b77bf1370fef739ce430debab297b463c7f34e))
* Fix file extension for logs uploaded to blob store ([49d8655](https://github.com/Azure/iotedge/commit/49d86554713daedb792c699f88266763666aae6f))
* Add ability to get status of logs upload request ([e7876eb](https://github.com/Azure/iotedge/commit/e7876eb508f671713fe5de97f04092a131b692ea))
* Put experimental features behind experimental flags ([9e6ea0c](https://github.com/Azure/iotedge/commit/9e6ea0c7df6568554cb6508ca56a8f9ae489b07b))

## Edge Hub
### Features
* Support for arm64 ([4fdfa40](https://github.com/Azure/iotedge/commit/4fdfa40686f9308f6f67d7662c50b6664c472994))
* Upstream performance improvements ([864b33d](https://github.com/Azure/iotedge/commit/864b33d6a038596aa6656a58f2f3d28ae4358cc4))
* Twin Manager v2 is now default ([96a0087](https://github.com/Azure/iotedge/commit/96a0087456bf982aad8f11020ab6d39d4b5f9e8b))
* Encrypt twins at rest ([075d5c0](https://github.com/Azure/iotedge/commit/075d5c0a39009eb9b0569e97c02ee1840dd5719f))
* Additional optional settings to limit upstream bandwidth usage

### Bug Fixes
* Fix IoT Hub name parsing in AMQP SASL Plain auth ([bb6c327](https://github.com/Azure/iotedge/commit/bb6c3271b035579ffb4e30af5fa4ab3637cf49f0))
* Set EdgeHub user id to UID 1000 explicitly ([cf40c16](https://github.com/Azure/iotedge/commit/cf40c165f4ffe777086a72bc3e278751be335cbd))
* Fix possible NRE in messages ([1c2efc6](https://github.com/Azure/iotedge/commit/1c2efc63fc4b949f3fd1dd8f06a42e453d5c1966))
* Fix edge case in checking twin version when storing ([663198c](https://github.com/Azure/iotedge/commit/663198cc30257a216f3c301ea5dfba0bf603e174))
* Forward product information for connected devices and modules ([749b9b7](https://github.com/Azure/iotedge/commit/749b9b7212b4257331db8f1641d9afd8a93bd30d))
* Configure MQTT protocol head to use num_procs * 2 threads. Improves stability on constrained devices. ([206568c](https://github.com/Azure/iotedge/commit/206568caa575cf9f358e5ff3ab4b6e24d082b7fa))
* Put experimental features behind experimental flags ([9e6ea0c](https://github.com/Azure/iotedge/commit/9e6ea0c7df6568554cb6508ca56a8f9ae489b07b))

## iotedged
### Features
* Update uTPM to support Resource Manager v2 ([a272069](https://github.com/Azure/iotedge/commit/a272069cc4f28aa1b724f50b3460f0eab13cad42))
* Return meaningful exit codes on failure ([62f3d44](https://github.com/Azure/iotedge/commit/62f3d44f9da239f5e6ef4a5637df14d93c8a5fe3))

### Bug Fixes
* Properly handle asynchronous errors when pulling images ([020ddbc](https://github.com/Azure/iotedge/commit/020ddbc7e73650956cb85dfa0dea152a89c44e60))
* Fix RPM packages for SUSE ([c16bc50](https://github.com/Azure/iotedge/commit/c16bc50731677040d2a371c1374aa6941b9a34d8))
* Don't lowercase the keys in `config.yaml` ([34df35a](https://github.com/Azure/iotedge/commit/34df35a3975767f9dcd5fc62f3f6bd80a5c63af5))
* Windows install script checks for container feature ([90f6368](https://github.com/Azure/iotedge/commit/90f63680bf19781ba09e9bbfaad26283cc7787b1))
* Do not reconfigure when provisioning from the backup ([b40ab5b](https://github.com/Azure/iotedge/commit/b40ab5b7e969e553fa868604f168eb0ca37e6194))

## Simulated Temperature Sensor
### Features
* Support for arm64 ([a9474e0](https://github.com/Azure/iotedge/commit/a9474e0fdc117533886d3bc32fd97cc11105d43d))

# 1.0.7.1 (2019-05-24)
* Fix regression in DPS use on Windows
* Stability improvements

## Edge Agent
### Bug Fixes
* Workaround `ObjectDisposedException` bug in C# SDK by exiting the process ([bbc8d3c](https://github.com/Azure/iotedge/commit/bbc8d3ce1ebc2583717295dfeeb1d737642e9946))

## Edge Hub
### Bug Fixes
* Workaround `ObjectDisposedException` bug in C# SDK by recreating the client ([e458e14](https://github.com/Azure/iotedge/commit/e458e14294d8c39f9e6f72e2c42418bfd298eeb2), [7598ef0](https://github.com/Azure/iotedge/commit/7598ef045c9b104dfdc758d270722007d981d1bb), [c608f38](https://github.com/Azure/iotedge/commit/c608f38b33652f4f2b10a97790fe155e39d9280a))

## iotedged
### Bug Fixes
* Fix bug preventing `iotedged` service starting when DPS provisioning is configured ([8a0f5c0](https://github.com/Azure/iotedge/commit/8a0f5c0ebb9489c98f591851e96e9f6766e031a9), [1ac1e94](https://github.com/Azure/iotedge/commit/1ac1e94298de332f180efe7ab343257207a77ebf))

# 1.0.7 (2019-05-06)
* Edge Agent pulls images before creating
* All processes in a container can authenticate with `iotedged`
* Provisioning: Symmetric key attestation method support
* `iotedge check` troubleshooting command
* Upgrade C# SDK to 1.20.1

## Edge Agent
### Features
* Agent pulls images before stopping ([57c6f7d](https://github.com/Azure/iotedge/commit/57c6f7d02c99634fc59f2f9a87fddc867691acb0), [4992833](https://github.com/Azure/iotedge/commit/4992833344f5fbf167c75af4b33b181e2b214692))
* Upgrade to version 1.20.1 of the C# SDK ([1637ff9](https://github.com/Azure/iotedge/commit/1637ff9303a162144f16b4c514859c247cc857fc))

### Bug Fixes
* Twin refresh timer logic is now a simple loop ([cb7af40](https://github.com/Azure/iotedge/commit/cb7af4090aca24d624e96ced572d6dc31b7c97c0))
* Add explicit timeout to `Edge Agent` <--> `iotedged` operations and more debug logs ([f2cb600](https://github.com/Azure/iotedge/commit/f2cb6003076cded75dec2dc87a3e79c23aa98fc9))

## Edge Hub
### Features
* Upgrade to version 1.20.1 of the C# SDK ([1637ff9](https://github.com/Azure/iotedge/commit/1637ff9303a162144f16b4c514859c247cc857fc))

### Bug Fixes
* Defaults to OptimizeForPerformance=false on arm32v7 ([43d47b0](https://github.com/Azure/iotedge/commit/43d47b04c4e70fd7c48a5b05f728925010f2e1ba))
* Limit MQTT thread count on arm32v7 ([2509438](https://github.com/Azure/iotedge/commit/2509438464cf9c7d99922ecd5e15caaf4e9ae242), [56a6db1](https://github.com/Azure/iotedge/commit/56a6db1f0faacf46162e2017a2f4344ac320c6e9))
* Process subscriptions from clients in batch ([20cb6c4](https://github.com/Azure/iotedge/commit/20cb6c46b9c26557a31a7c22261507ed1d3ebe78))

## iotedged
### Features
* Support for DPS symmetric key provisioning ([b7adfff](https://github.com/Azure/iotedge/commit/b7adfffefe85cef84e27302aa0c8f00a3e8a81c2))
* All modules processes are authorized to connect ([777aec1](https://github.com/Azure/iotedge/commit/777aec16a673cad2407bf75291d29b6d5e71ef25))
* Add `iotedge check` troubleshooting command ([1d74b97](https://github.com/Azure/iotedge/commit/1d74b97e1893134d6989366145e694dedd162f0f))
* Use CAB file for Windows installation ([ce232a8](https://github.com/Azure/iotedge/commit/ce232a8f8ef98f2b22964242ae34dc810e02672a))

### Bug Fixes
* Encode deviceid/moduleid for IoT Hub operations ([bb10be0](https://github.com/Azure/iotedge/commit/bb10be01360ed7393260351af2c6e8ad7498346d))
* Load encryption key before generating it ([9174a89](https://github.com/Azure/iotedge/commit/9174a896f7cca21c3dd4ae84c9e097d0d20305d5))

## Simulated Temperature Sensor
### Features
* Add SendData and SendInterval twin configuration ([7dc7041](https://github.com/Azure/iotedge/commit/7dc7041f790ebe323d720913782e8085f1f65c21))
* Upgrade to version 1.20.1 of the C# SDK ([1637ff9](https://github.com/Azure/iotedge/commit/1637ff9303a162144f16b4c514859c247cc857fc))

## Functions Binding
### Features
* Upgrade to version 1.20.1 of the C# SDK ([1637ff9](https://github.com/Azure/iotedge/commit/1637ff9303a162144f16b4c514859c247cc857fc))

# 1.0.6.1 (2019-02-04)

## iotedged
### Bug Fixes
* Reverts name sanitization of the common name on generated certificates ([078bda7](https://github.com/Azure/iotedge/commit/078bda7b86b55e8017077b8e2490dede1f8703dc))

# 1.0.6 (2019-01-31)
* Stability and reliability fixes

## Edge Agent
### Features
* Update to .NET Core 2.1.6 ([d2023be](https://github.com/Azure/iotedge/commit/d2023bec1bf362cd78d2aff06178b2f18d62cb7c))

### Bug Fixes
* Fix module restart logic when Edge Agent clock is off ([72f7112](https://github.com/Azure/iotedge/commit/72f7112113320fdc8b3d546a24a880d46fb4cd74))
* Use HTTPS proxy on Linux and Windows ([fceef9f](https://github.com/Azure/iotedge/commit/fceef9f35e3c3021523201920f33e06398f26ebb))

## Edge Hub
### Features
* Update to .NET Core 2.1.6 ([d2023be](https://github.com/Azure/iotedge/commit/d2023bec1bf362cd78d2aff06178b2f18d62cb7c))
* Support X509 certificate authentication by default for downstream devices ([4a46290](https://github.com/Azure/iotedge/commit/4a46290d2c2bd309ea9bbf7c697b71851923a08e))
* New improved Twin manager - in preview and not enabled by default ([d99f8ff](https://github.com/Azure/iotedge/commit/d99f8ff085799092fb665466ae7ed7beeffceea3))

### Bug Fixes
* Use HTTPS proxy on Linux and Windows ([eb75f34](https://github.com/Azure/iotedge/commit/eb75f346e19a21953c46f6cc0c2a4c77115d13e9))
* Allow modules on Edge devices with no device scope to connect to Edge Hub ([761254f](https://github.com/Azure/iotedge/commit/761254fa948d95d6de022c6b3c3e5c8e77594679))
* Handle clients with special characters ([82ce72e](https://github.com/Azure/iotedge/commit/82ce72e49a20bdd4feec417c2f7c021af8fc55c4))
* Fix potential for dropped messages when device is rebooted ([88fd5ab](https://github.com/Azure/iotedge/commit/88fd5abc2a817d32adda1338685c0f1f9e1ff744))

## iotedged
### Bug Fixes
* Sort serialization of environment variables in config.yaml ([0e6a402](https://github.com/Azure/iotedge/commit/0e6a402fecf9f11c3f8afff7713352ddc165a234))
* Support installing iotedged on localized Windows installations ([d9b12c9](https://github.com/Azure/iotedge/commit/d9b12c96168222d23fdf1ebc122f3a7ada6fafd2))
* Reinstate "nat" as the Moby network for Windows containers ([913678a](https://github.com/Azure/iotedge/commit/913678ac7e7f65f4f954ba898a4325efdc05dc5a))

# 1.0.5 (2018-12-17)
* Support Windows 10 1809 (RS5)
* Improved error messages in `iotedge`/`iotedged`
* Stability and reliability fixes

## Edge Agent
### Features
* Parallelize stopping modules on shutdown ([271e930](https://github.com/Azure/iotedge/commit/271e930d5ad5fe5fa17d05fee25f55d4cc6ed2a3))

### Bug Fixes
* Avoid caching backup.json on every reconcile ([2cea69f](https://github.com/Azure/iotedge/commit/2cea69f97fb24ebbacdfeec73bb805bfd61f85f7))

## Edge Hub
### Features
* Drain messages from disconnected clients to IoT Hub ([d3f801b](https://github.com/Azure/iotedge/commit/d3f801ba27acea42664876d3dd70fd695f69de5e))
* Make device/module client operation timeout configurable -- helps slow connections ([6102e31](https://github.com/Azure/iotedge/commit/6102e31b660296054e117de3787c149ac1bc627e))
* Resync service identity if client request cannot be authenticated ([677e16d](https://github.com/Azure/iotedge/commit/677e16d96bde53c88459e61ec72b67cd3ef29a3a))
* Enable support for X.509 thumbprint and CA auth for downstream devices - not enabled by default ([187e3df](https://github.com/Azure/iotedge/commit/187e3dfc526a6b49da09d69e75eef7e4454f04d7))
* Add support for X.509 auth for HTTP and MQTT over Websockets - not enabled by default ([9b56f3d](https://github.com/Azure/iotedge/commit/9b56f3d49444d122e2263b494512557576b19b29))
* AMQP and AMQP+WS support for X.509 authentication - not enabled by default ([875776c](https://github.com/Azure/iotedge/commit/875776c71ea6a551caa4150f2a251086c36e2196))
* Allow multiplexing client connections over AMQP ([93be534](https://github.com/Azure/iotedge/commit/93be5343561362c6244e5e42e09b413baecd53c3))

### Bug Fixes
* Fix NRE in TwinManager ([29f5b74](https://github.com/Azure/iotedge/commit/29f5b745057663966eb9fd27d3c0f0b5c9b86c79))
* Handle NRE thrown by device SDK ([5f5fd67](https://github.com/Azure/iotedge/commit/5f5fd67631d69e95c17c6bc32e9f1926da237034))
* Fix obtaining upstream connection when offline ([75e7968](https://github.com/Azure/iotedge/commit/75e796826d7a8c8161b0189e24da2cc6f27655d8))
* Fix MessageStore initial offset after restart ([81f93dc](https://github.com/Azure/iotedge/commit/81f93dc408b24cb1f5cc7bbcbdaf9133a5e24937))
* Add timeout / cancellation support to Store apis ([0eb279b](https://github.com/Azure/iotedge/commit/0eb279beaf5175d5b4e8342f6d313411c748eae7))

## iotedged
### Features
* Add identity certificate endpoint to workload API ([40f1095](https://github.com/Azure/iotedge/commit/40f10950dc65dd955e20f51f35d69dd4882e1618))
* Add module list to workload API ([5547161](https://github.com/Azure/iotedge/commit/554716151f8802a8f1c2c38bdd5a6914fe2191a5))
* Support Unix Domain Sockets on Windows :tada: ([b1ee469](https://github.com/Azure/iotedge/commit/b1ee46916514779b5d8f001bfd2a2f4fdf2bf141))
* Move network-online.target to Wants from Requires in systemd unit ([c525acc](https://github.com/Azure/iotedge/commit/c525acc28a20c3f6c4198ea8db48a76ad61e4d2c))
* Add more informative error messages ([326ef8c](https://github.com/Azure/iotedge/commit/326ef8c0075e66834c82b36fc5551b8ef74f0098))
* Add support for x.509 v3 extensions Subject and Auth Key Identifiers ([9b98780](https://github.com/Azure/iotedge/commit/9b987801a836955ffdcf0b5b6ea9f96db79159df))
* libiothsm-std now includes an so version ([5667a9f](https://github.com/Azure/iotedge/commit/5667a9fd06b6570ab3fc4a4d6b45e1d5c3988638))
* Remove write access for BUILTIN\Users in `C:\ProgramData\iotedge` ([d6b8c3a](https://github.com/Azure/iotedge/commit/d6b8c3a89a7c9b4964651490f72007cf72183112))
* Update Windows images to RS5 ([f72a238](https://github.com/Azure/iotedge/commit/f72a238ecd65dbdd627c0dd474f7a8f7ab1876bc))
* Enable TLS 1.2 for Invoke-WebRequest ([e93e707](https://github.com/Azure/iotedge/commit/e93e70721ea2089a424ec13e5fcdc5daa7d05018))
* Start service automatically on Windows startup when using Windows containers on Moby ([f72a238](https://github.com/Azure/iotedge/commit/f72a238ecd65dbdd627c0dd474f7a8f7ab1876bc))
* Restart service on crash ([f72a238](https://github.com/Azure/iotedge/commit/f72a238ecd65dbdd627c0dd474f7a8f7ab1876bc))
* Windows installer support for offline installation (using the `-OfflineInstallationPath parameter) ([8cec3d5](https://github.com/Azure/iotedge/commit/8cec3d50f9035160e3bd3e537f84ab48a6e28f58))
* Windows installer support for reusing previous config.yaml on reinstall ([82b82cc](https://github.com/Azure/iotedge/commit/82b82ccffdaeed408b04803ea2679e9b626d15c9))
* `iothsm.dll` now configured to use physical TPM instead of emulator

### Bug Fixes
* Fix potential race in management API list modules ([645545a](https://github.com/Azure/iotedge/commit/645545af22caeb2a6a4883a5adb0881eb5a2ca0f))
* Update Windows installer to create user-defined network for modules ([6d5b95a](https://github.com/Azure/iotedge/commit/6d5b95a7be94daa0f297c4288485301cb988f9f0))

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
