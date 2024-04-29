# 1.4.34 (2024-04-10)

Only Docker images are updated in this release. The daemon remains at version 1.4.33.

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.33 to match the daemon)

# 1.4.33 (2024-03-12)

## Edge Agent
### Bug fixes
* Ensure agent and hub use logger for stack traces by @Gunni ( [4ae6c29](https://github.com/Azure/iotedge/commit/4ae6c29c771e1ce087c15ba6079c19d2f9f44b1e) )

## Edge Hub
### Bug fixes
* Ensure agent and hub use logger for stack traces by @Gunni ( [4ae6c29](https://github.com/Azure/iotedge/commit/4ae6c29c771e1ce087c15ba6079c19d2f9f44b1e) )

## aziot-edge
### OS support
* Add support for Snap amd64/arm64 packages ( [c38e0c8](https://github.com/Azure/iotedge/commit/c38e0c896d7b1bc7dc338b8ec0637daaeb6d27b3) )

### Bug fixes
* Fix apt purge --autoremove on Debian/Ubuntu ( [6c34f4b](https://github.com/Azure/iotedge/commit/6c34f4bdbd36de74f98ce88213ae0b11b9ffb909) )

## aziot-identity-service
### OS support
* Add support for Snap amd64/arm64 packages ( [9743701](https://github.com/Azure/iot-identity-service/commit/9743701f21e729984200968e909cdd639b1f0799) )

### Features
* Add packages for debug symbols ( [0cea2bd](https://github.com/Azure/iot-identity-service/commit/0cea2bd36466ec843056715441949990ab836ce5) )

### Bug fixes
* Fix apt purge --autoremove on Debian/Ubuntu ( [014edf1](https://github.com/Azure/iot-identity-service/commit/014edf1992347a8ca54662c90df62003b375ed40) )

# 1.4.32 (2024-02-14)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.27 to match the daemon)

# 1.4.31 (2024-01-27)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.27 to match the daemon)

# 1.4.30 (2024-01-27)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.27 to match the daemon)

# 1.4.29 (2024-01-11)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.27 to match the daemon)

# 1.4.28 (2024-01-10)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.27 to match the daemon)

# 1.4.27 (2023-12-07)

## Edge Hub
### Bug fixes
* Add support for GetCountFromStartKey for InMemoryDbStore ( [f1a9da3](https://github.com/Azure/iotedge/commit/f1a9da3b088ca39953265ce7e2b76d7b08cf5829) )

## aziot-edge
### Bug fixes
* Parse default edged path from environment variable by @ef4203 ( [1f048bf](https://github.com/Azure/iotedge/commit/1f048bfa7d983d3e80072ba7a4e7429a77c110d8) )
* Remove Ubuntu 18.04 support ( [765ec2d](https://github.com/Azure/iotedge/commit/765ec2d71a66b458e1fa5d446617ff5d5cca40df) )

## aziot-identity-service
### Bug fixes
* Remove Ubuntu 18.04 support ( [ea88b83](https://github.com/Azure/iot-identity-service/commit/ea88b834555ec1c5414d29e9238e04e8ca8d3184) )
* Fix nullptr deref when decoding EST PKCS#7 response ( [3fd2073](https://github.com/Azure/iot-identity-service/commit/3fd2073c6fe58b009ef9a3b73d14103b55f283a5) )
* Only create PKCS#11 AES keys if AES-GCM is supported ( [79aae50](https://github.com/Azure/iot-identity-service/commit/79aae5062f0057df7871fb2897451e9b58daf0ec) )

## Other fixes
* Upgrade Functions sample to remove dependency on .NET Core 3.1 ( [c38aa54](https://github.com/Azure/iotedge/commit/c38aa547542d7b9af099050ee19bf8f384a1bb5a) )

# 1.4.26 (2023-12-01)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.20 to match the daemon)

# 1.4.25 (2023-11-15)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.20 to match the daemon)

# 1.4.24 (2023-10-25)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.20 to match the daemon)

# 1.4.23 (2023-10-13)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.20 to match the daemon)

# 1.4.22 (2023-10-11)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.20 to match the daemon)

# 1.4.21 (2023-09-29)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.20 to match the daemon)

# 1.4.20 (2023-09-18)

## Edge Agent
### Bug fixes
* Fix container restart policy deserialization ( [bd05d4d](https://github.com/Azure/iotedge/commit/bd05d4d8346b9cc3b029c74468a1db3eefa7287c) )

## aziot-edge
### Bug fixes
* Add support for 'prefer_module_identity_cache' option ( [9c7dbdd](https://github.com/Azure/iotedge/commit/9c7dbdd4b9aa9eb54394fec3f13bc3ff3fe66d1e) )
* Fix error in CLI warning message ( [978ccaa](https://github.com/Azure/iotedge/commit/978ccaa43efbddc55f89527934a5afe5227deef8) )

## aziot-identity-service
### Bug fixes
* Add support for 'prefer_module_identity_cache' option ( [137258d](https://github.com/Azure/iot-identity-service/commit/137258ddd57bac138b3b0333b2e16a99427a73bf) )
* Update EL package configuration to fix a conflict with distro's tpm2-tss package ( [d644195](https://github.com/Azure/iot-identity-service/commit/d6441951c477c072cd56bc9e8d487b3e790255dc) )
* Remove socket path if it is a directory instead of a file ( [ed69cc4](https://github.com/Azure/iot-identity-service/commit/ed69cc4d34d99c3d655b297455329e9a5005b732) )

# 1.4.19 (2023-09-13)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.16 to match the daemon)

# 1.4.18 (2023-08-09)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.16 to match the daemon)

# 1.4.17 (2023-08-08)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.16 to match the daemon)

# 1.4.16 (2023-07-28)

## Edge Hub
### Bug fixes
* Ensure database shuts down properly before Edge Hub closes ( [238c121](https://github.com/Azure/iotedge/commit/238c12176512b676dd24cbc6d2b55323faf13efb) )

## aziot-edge
### Bug fixes
* Ignore 'systemd daemon-reload' errors when purging debian package ( [291d716](https://github.com/Azure/iotedge/commit/291d716f5f784a80205962804037cbf72fb99acb) )
* Patch vulnerabilities in cargo dependencies ( [9e71341](https://github.com/Azure/iotedge/commit/9e713419235666f04ee5fa8823386890ef087193) )
* Make RHEL8 package depend on moby-engine or docker-ce ( [3a2e68e](https://github.com/Azure/iotedge/commit/3a2e68e378301fd132d7057dbdb0de7bcb9b181b) )

## aziot-identity-service
### Bug fixes
* Ignore 'systemd daemon-reload' errors when purging debian package ( [7856c23](https://github.com/Azure/iot-identity-service/commit/7856c232a24e7503b32957737b4a88ecabf33063) )
* Patch vulnerabilities in cargo dependencies ( [67fa660](https://github.com/Azure/iot-identity-service/commit/67fa660f96637b57b35ef6049802b7e2a8d1844d) )

# 1.4.15 (2023-07-11)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.10 to match the daemon)

# 1.4.14 (2023-06-23)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.10 to match the daemon)

# 1.4.13 (2023-06-15)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.10 to match the daemon)

# 1.4.12 (2023-06-14)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.10 to match the daemon)

# 1.4.11 (2023-05-26)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.10 to match the daemon)

# 1.4.10 (2023-05-01)

Beginning with this release we are publishing installable packages for Red Hat Enterprise Linux 9 (amd64) on Microsoft's [Linux package repository](https://packages.microsoft.com/docs/readme.txt).

**Note:** On RHEL 9 the IoT Edge security subsystem has been tested with openssl 3.0. It may not function properly if older versions of openssl are also present on the device. If you previously installed openssl 1.1 in combination with an earlier version of IoT Edge then we would recommend removing both and starting fresh to avoid potential incompatibilities.

## Base image updates

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics

## Edge Agent
### Bug fixes
* Update versions of .NET dependencies to patch security vulnerabilities ( [82ca5e8](https://github.com/Azure/iotedge/commit/82ca5e8eb082a3d5ebf8276fc0109f923c022ad2) )
* Update version of Azure IoT SDK to fix a memory leak ( [d98f43c](https://github.com/Azure/iotedge/commit/d98f43c90d456f51903ee2fa3f7266083086d739) )
* Optionally detect and remove orphaned module identities when a new deployment is received ( [3bac802](https://github.com/Azure/iotedge/commit/3bac80274305c7f2d4af92c161bead2c486d6820) )

## Edge Hub
### Bug fixes
* Update versions of .NET dependencies to patch security vulnerabilities ( [82ca5e8](https://github.com/Azure/iotedge/commit/82ca5e8eb082a3d5ebf8276fc0109f923c022ad2) )
* Update version of Azure IoT SDK to fix a memory leak ( [d98f43c](https://github.com/Azure/iotedge/commit/d98f43c90d456f51903ee2fa3f7266083086d739) )
* Optionally check for server cert expiry at the given interval ( [fbe35da](https://github.com/Azure/iotedge/commit/fbe35dad396d87b422c4ea34c6062ab9948d791b) )

## aziot-edge
### OS support
* Add support for RHEL 9 amd64 ( [32f7481](https://github.com/Azure/iotedge/commit/32f7481b40bdac21953d9e78b29914e0f1ae8d6d) )

### Bug fixes
* Add a timeout to prevent `iotedge support-bundle` from hanging in certain circumstances ( [f7dd1aa](https://github.com/Azure/iotedge/commit/f7dd1aaf5fa59da03eee90a2eb1dec6572575168) )
* Relax padding requirement in symmetric keys ( [907eef1](https://github.com/Azure/iotedge/commit/907eef17af5242dccae4ac90daf3106451bda5b1) )
* Fix memory and swap information reported by `iotedge check` and Edge Agent ( [b29d736](https://github.com/Azure/iotedge/commit/b29d73632dad88a495c2b93f767e241ab3c3d1ef) )
* Add comment to config template about quickstart Edge CA ( [a4196a4](https://github.com/Azure/iotedge/commit/a4196a4d4022cf8917f0a17c71979b4b5dc74843) )
* Update guidance in `iotedge config apply` warning message ( [86b8e69](https://github.com/Azure/iotedge/commit/86b8e698802cde3a427ee62618463642359b2275) )
* Update version of openssl crate to patch security vulnerabilities ( [3b8b9e3](https://github.com/Azure/iotedge/commit/3b8b9e3efe2c797d9a3db56096c77e527a286d90) )

## aziot-identity-service
### OS support
* Add support for RHEL 9 amd64 ( [24f227d](https://github.com/Azure/iot-identity-service/commit/24f227d644ead36ab8fb4195f6203de4603097d3) )

### Bug fixes
* Relax padding requirement in symmetric keys ( [77ca573](https://github.com/Azure/iot-identity-service/commit/77ca57305b070dba89dcddb948eec5c8f1cb029c) )
* Update version of openssl crate to patch security vulnerabilities ( [df1885b](https://github.com/Azure/iot-identity-service/commit/df1885b6a78ef6491673c65659ead90a2740427a) )

# 1.4.9 (2023-02-14)

Beginning with this release we are publishing installable packages for Ubuntu 22.04 (amd64, arm64) on Microsoft's [Linux package repository](https://packages.microsoft.com/docs/readme.txt).

**Note:** On Ubuntu 22.04 the IoT Edge security subsystem has been tested with openssl 3.0. It may not function properly if older versions of openssl are also present on the device. If you previously installed openssl 1.1 in combination with an earlier version of IoT Edge then we would recommend removing both and starting fresh to avoid potential incompatibilities.

## Base image updates

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics

## Edge Agent
### Bug fixes
* Fix incorrectly reported metrics on a module's expected and actual running time ( [94f8072](https://github.com/Azure/iotedge/commit/94f807209964410ee17329be85a7835f00354b07) )

## aziot-edge
### OS support
* Add support for Ubuntu 22.04 amd64, arm64v8 ( [b4b54da](https://github.com/Azure/iotedge/commit/b4b54da011b73d2fe3182974ed11b0a6a27a1d38) )

### Bug fixes
* Enable >4GB files in support_bundle ZIP writer ( [cea876f](https://github.com/Azure/iotedge/commit/cea876ff651987e45d024160aaa66a364c3213c4) )
* Update cargo dependencies to take security updates ( [a372eca](https://github.com/Azure/iotedge/commit/a372eca9d9ade13c3d6a2df5b554ba1d2fbcd21e) )
* Update to the latest version of aziot-identity-service ( [37f51c2](https://github.com/Azure/iotedge/commit/37f51c2a39f0e95ea510eb975c38c659723379a4) )
* Fix `iotedge restart` command to correct a problem with workload sockets ( [08dfac5](https://github.com/Azure/iotedge/commit/08dfac5fb9dd4de02f20feb7014040c8295523e5) )

## aziot-identity-service
### OS support
* Add support for Ubuntu 22.04 amd64, arm64v8 ( [ea9e476](https://github.com/Azure/iot-identity-service/commit/ea9e47617b9d322d15489745d3ba11ac7e666ee9) )

### Bug fixes
* Retry with exponential backoff when IoT Hub throttles ( [a6aacda](https://github.com/Azure/iot-identity-service/commit/a6aacdaaadde4052f02eb828bd7f6ef583a550fd) )
* Update cargo dependencies to take security updates ( [b3de517](https://github.com/Azure/iot-identity-service/commit/b3de51744e277ae0f517c6d1d908b9afcbd68142) )
* Use fair mutex to fix request ordering problem ( [03e383e](https://github.com/Azure/iot-identity-service/commit/03e383e390670bd75e7aab6e58e363ae2276f437) )

# 1.4.8 (2023-01-26)

## Edge Agent
### Bug fixes
* Use ISO 8601 for UTC timestamps sent to IoT Hub ( [0ab44e1](https://github.com/Azure/iotedge/commit/0ab44e170c9bc6a714aa5632fb29962d165205d6) )

## Edge Hub
### Bug fixes
* Eliminate 30 sec delay when M2M ack is interrupted by disconnect ( [e32cfce](https://github.com/Azure/iotedge/commit/e32cfce85fe58acdd327a63582741ea4e8914d01) )

## aziot-edge
### Bug fixes
* Use ISO 8601 for UTC timestamps sent to IoT Hub ( [0ab44e1](https://github.com/Azure/iotedge/commit/0ab44e170c9bc6a714aa5632fb29962d165205d6) )
* Bump iot-identity-service to 1.4.2

# 1.4.7 (2023-01-10)

## Edge Agent
### Bug fixes
* Update to Newtonsoft.Json 13.0.2 ( [f2b95bf](https://github.com/Azure/iotedge/commit/f2b95bf4a069af7e30ad6c5ff2eac25f450d2f3a) )

## Edge Hub
### Bug fixes
* Update to Newtonsoft.Json 13.0.2 ( [f2b95bf](https://github.com/Azure/iotedge/commit/f2b95bf4a069af7e30ad6c5ff2eac25f450d2f3a) )

## Base image updates

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.3 to match the daemon)

# 1.4.6 (2022-12-30)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.3 to match the daemon)

# 1.4.5 (2022-12-16)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.3 to match the daemon)

# 1.4.4 (2022-12-01)

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version 1.4.3 to match the daemon)

# 1.4.3 (2022-11-22)

## Edge Agent
* Fix bug causing Edge Agent to delay sending reported properties to IoT Hub by 1 hour ( [e43cdc9](https://github.com/Azure/iotedge/commit/e43cdc91a7969d8029c679d2b169f0682bf65e18) )
* Fix edgeagentuser's login shell ( [6274476](https://github.com/Azure/iotedge/commit/62744766ec321012365e746310a300853ced3c08) )
* Make client timeout configurable for management API ( [7a379d3](https://github.com/Azure/iotedge/commit/7a379d3177f6af68364684db338e358d0a099150), [8afaa3a](https://github.com/Azure/iotedge/commit/8afaa3a0d5c72ebe7b35dff21742ed5ed9843033) )

## Edge Hub
* Call IoT Device SDK CloseAsync before Dispose ( [8787301](https://github.com/Azure/iotedge/commit/8787301b82e487e8f83bb4f616395e4cb62b9844) )
* Upgrade DotNetty and set a timeout for shutdown calls to mitigate hangs ( [15e72bb](https://github.com/Azure/iotedge/commit/15e72bb852a62f09f1bbb7d2b060fbea86765bc0) )
* Fix edgehubuser's login shell ( [6274476](https://github.com/Azure/iotedge/commit/62744766ec321012365e746310a300853ced3c08) )

## aziot-edge
* Make iotedge check respect journald as valid log rotation setting ( [3a39460](https://github.com/Azure/iotedge/commit/3a394606d79af99b82acf9708d0ad404bec8c9f8) )

## Other fixes
* Upgrade Azure Functions sample's base image ( [c38c61d](https://github.com/Azure/iotedge/commit/c38c61d08b0a23e1c0c9e8b4ddbdbc9c57bd3adb) )
* Upgrade Newtonsoft.Json in samples and Azure Functions binding ( [a5ae82b](https://github.com/Azure/iotedge/commit/a5ae82ba64fb6e43b0e6df172a293381c9adb3d4) )

# 1.4.2 (2022-10-04)

## Edge Hub
### Bug Fixes
* Update dependency to fix OOM bug ( [906786c](https://github.com/Azure/iotedge/commit/906786c52526cca4e90aaaec0de7c389e890a387) )

## aziot-edge
### Bug Fixes
* Fix confusing log message in image garbage collection ( [736116b](https://github.com/Azure/iotedge/commit/736116b7ac7145730363c7dd55e28e64489ac4ae) )

## Base image updates
The following Docker images were updated because their base images changed:
   * azureiotedge-agent
   * azureiotedge-hub
   * azureiotedge-simulated-temperature-sensor
   * azureiotedge-diagnostics

# 1.4.1 (2022-09-09)

## aziot-edge
* Bump iot-identity-service to fix regression in TPM authentication key index ( [fd90024](https://github.com/Azure/iotedge/commit/fd9002452871fc0601798e74499c5f3acbd09574) )

# 1.4.0 (2022-08-26)

## What's new in 1.4?

The 1.4 version is the latest long-term support (LTS) version of IoT Edge. It will be serviced with fixes for regressions and critical security issues through November 12, 2024 ([product lifecycle](https://docs.microsoft.com/lifecycle/products/azure-iot-edge)). In addition to long-term servicing, it includes the following improvements.
* Automatic cleanup of unused Docker images ([doc](https://docs.microsoft.com/azure/iot-edge/production-checklist?view=iotedge-1.4#configure-image-garbage-collection))
* Ability to pass a custom json payload to DPS on provisioning ([doc](https://docs.microsoft.com/azure/iot-dps/how-to-send-additional-data#iot-edge-support))
* Option to download all modules in a deployment before (re)starting any ([doc](https://docs.microsoft.com/azure/iot-edge/production-checklist?view=iotedge-1.4#configure-how-updates-to-modules-are-applied))
* Use of the TCG TPM2 Software Stack which enables TPM hierarchy authorization values, specifying the TPM index at which to persist the DPS authentication key, and accommodating more TPM configurations ([doc](https://github.com/Azure/iotedge/blob/897aed8c5573e8cad4b602e5a1298bdc64cd28b4/edgelet/contrib/config/linux/template.toml#L262-L288))

**With this release, the 1.3.x release is no longer serviced with bug fixes and security patches.**

## Upgrade notes

When upgrading to 1.4 you should be aware of the following changes:
* Automatic cleanup of unused Docker images is on by default
* If upgrading from 1.0 or 1.1 then refer to the notes on [updating IoT Edge to the latest release](https://docs.microsoft.com/azure/iot-edge/how-to-update-iot-edge#special-case-update-from-10-or-11-to-latest-release)

## Edge Agent
* Fix bug where Edge Agent is updated without backing image  ( [72e5d648c](https://github.com/Azure/iotedge/commit/72e5d648cc25c05d64dfd52010b7178af772445f) )
* Fix user creation for edgeAgent and edgeHub  ( [388ec1a34](https://github.com/Azure/iotedge/commit/388ec1a341cf18fce3379c6750e62591963c8624) )
* Add total memory to device metadata  ( [683a2dde6](https://github.com/Azure/iotedge/commit/683a2dde66fe970e98bc2b187e3419a7e71f1302) )
* Support feature flag `ModuleUpdateMode`  ( [303b3fdcc](https://github.com/Azure/iotedge/commit/303b3fdcc065c5f6e2e8087e4f92baaafb03b85a) )
* Update NewtonSoft to 13.0.1  ( [84e883779](https://github.com/Azure/iotedge/commit/84e8837797e84a8e2ae198e42ecffb1c7b7ae546) )
* Remove docker mode  ( [40824ed28](https://github.com/Azure/iotedge/commit/40824ed28ee6c51eb96c9459513694ae7fc1c018) )


## Edge Hub
* Fix user creation for edgeAgent and edgeHub  ( [388ec1a34](https://github.com/Azure/iotedge/commit/388ec1a341cf18fce3379c6750e62591963c8624) )


## aziot-edge
* Run `cargo update` everywhere  ( [82d1c12c6](https://github.com/Azure/iotedge/commit/82d1c12c6f283997f80ea02e5b9f9b20a9c3a355) )
* Image garbage collection for iotedge  ( [f48335d68](https://github.com/Azure/iotedge/commit/f48335d684f7c80723d3801b45ec4704188104b0) )
* Allow socket throttling limits to be configurable  ( [ba7052fd3](https://github.com/Azure/iotedge/commit/ba7052fd3e694b9cb6969b02f2f4356e2e527e9c) )
* Support privileged modules specified without `CAP_CHOWN` and `CAP_SETUID`  ( [d0470e2e6](https://github.com/Azure/iotedge/commit/d0470e2e6775b3e8f271474607891f6ccf834bb7) )
* Fix creation and cleanup of edgeagentuser and edgehubuser  ( [89801b4d9](https://github.com/Azure/iotedge/commit/89801b4d99b085e8b23ed1bfb6a2fd7805709984) )
* Fix user creation for edgeAgent and edgeHub  ( [388ec1a34](https://github.com/Azure/iotedge/commit/388ec1a341cf18fce3379c6750e62591963c8624) )
* Add total memory to device metadata  ( [683a2dde6](https://github.com/Azure/iotedge/commit/683a2dde66fe970e98bc2b187e3419a7e71f1302) )
* Trim leading `$` from server cert SANs  ( [9a6f39bcd](https://github.com/Azure/iotedge/commit/9a6f39bcdba50d4fdea91d3c30e777f8c2a83b9d) )
* Run `cargo update` everywhere  ( [96566c1d3](https://github.com/Azure/iotedge/commit/96566c1d33ce062f9273bd6d58db5cb9974e8660) )
* Include tpmd configuration section from IIS  ( [0a65c31a7](https://github.com/Azure/iotedge/commit/0a65c31a70b480ba85563834d296e60313a9e0a2) )
* Update version to 1.4.0  ( [1b3f818c2](https://github.com/Azure/iotedge/commit/1b3f818c2eecd08d9442eee98f1b57f5502f166b) )
* Support DPS custom allocation payloads  ( [b428ac9f4](https://github.com/Azure/iotedge/commit/b428ac9f438373bf8f29d92fe739261ad9eadff1) )
* Socket Activation for Mariner Package Builds  ( [6ac5577fd](https://github.com/Azure/iotedge/commit/6ac5577fd8ba65ae15715e72933fe5eec66619b1) )
* Upgrade to latest Rust version  ( [9a5ebddcf](https://github.com/Azure/iotedge/commit/9a5ebddcf0a4114e973d24ab8e4ea28e31e6efb1) )
* Correct container runtime status code propagation  ( [fe3137061](https://github.com/Azure/iotedge/commit/fe31370610753ed39d07b3720ea993c1413ad8fd) )
* Enable Edge CA auto-renewal by default  ( [279145c0a](https://github.com/Azure/iotedge/commit/279145c0a011b754b8ea8cd7bacc44f4c4bf6893) )
* Do not rename configuration items for SystemInfo  ( [4c4717e83](https://github.com/Azure/iotedge/commit/4c4717e8315a79b6e043639f4eae3a00fe6c8088) )


# 1.3.0 (2022-06-24)

## What's new in 1.3?

The 1.3 release is the next stable release after the 1.2 and includes the following in preparation for the next LTS:
* OS support changes
* System modules based on .NET 6 with Alpine as the base layer
* Required use of TLS 1.2 by default
* Ability to configure device identity, EST identity, and Edge CA certificate auto-renewal before expiration using `config.toml`, addresses https://github.com/Azure/iotedge/issues/5787, https://github.com/Azure/iotedge/issues/5788, and https://github.com/Azure/iot-identity-service/issues/300
* Added a check for `iotedge config apply` to detect hostname changes to prevent mismatch between configuration and _edgeHub_ server certificate, addresses https://github.com/Azure/iotedge/issues/5773 and https://github.com/Azure/iotedge/issues/6276
* Updates to the rust-based components to use tokio 1.0
* Various bug fixes

**With this release the 1.2.x is no longer serviced with bug fixes and security patches.**

## Upgrade notes

### Require TLS 1.2 by default

You can configure Edge Hub to still accept TLS 1.0 or 1.1 connections via the [SslProtocols environment variable](https://github.com/Azure/iotedge/blob/main/doc/EnvironmentVariables.md#edgehub).  Please note that support for [TLS 1.0 and 1.1 in IoT Hub is considered legacy](https://docs.microsoft.com/azure/iot-hub/iot-hub-tls-deprecating-1-0-and-1-1) and may also be removed from Edge Hub in future releases. To avoid future issues, use TLS 1.2 as the only TLS version when connecting to Edge Hub or IoT Hub.

### MQTT broker preview removed

The preview for the experimental MQTT broker in Edge Hub 1.2 has ended and is not included in Edge Hub 1.3. We are continuing to refine our plans for an MQTT broker based on feedback received. In the meantime, if you need a standards-compliant MQTT broker on IoT Edge, consider deploying an open-source broker like [Mosquitto](https://mosquitto.org/) as an IoT Edge module.

### Certificate renewal feature detail

You can have IoT Edge proactively renew device identity (for authentication to IoT Hub and DPS), Edge CA, and EST identity certificates by configuring a few basic options in the `config.toml`. Use this feature along with an EST server like [GlobalSign IoT Edge Enroll](https://www.globalsign.com/en/iot-edge-enroll) or [DigiCert IoT Device Manager](https://www.digicert.com/iot/iot-device-manager) to automate certificate renewals customized to your needs.

For example, adding the below configuration enables device identity certificate auto-renewal when the certificate is at 80% of its lifetime, retry at increment of 4% of lifetime, and rotate the private key:

```
[provisioning.attestation.identity_cert.auto_renew]
rotate_key = true
threshold = "80%"
retry = "4%"
```

To enable the certificate renewal feature, changes were made to consolidate and improve IoT Edge's certificate management system. There are some important differences in 1.3 compared to 1.2:
- All modules restart when Edge CA certificate is renewed. This is necessary so that each module receives the updated trust bundle with the new CA certificate. By default, and when there's no specific `auto_renew` configuration, Edge CA renews at 80% certificate lifetime and so modules would restart at that time.
- The device identity certificate no longer renews when reprovisioned within 1 day of certificate expiry. This old behavior in 1.2 is removed because it causes authentication errors with IoT Hub or DPS when using X.509 thumbprint authentication, since the new certificate comes with a new thumbprint that the user must manually update in Azure. In 1.3, device identity automatic renewal must be explicitly enabled similar to example above and should only be used with DPS X.509 CA authentication.
- The device identity certificate no longer renews when reprovisioned after certificate expiry. The reason for this change is same as above: device identity certificates do not renew by default since it causes issues with X.509 thumbprint authentication. 

## OS support
* Adding RedHat Enterprise Linux 8 for AMD and Intel 64-bit architectures.
* Adding Debian 11 (Bullseye) for ARM32v7 ( [Generally available: Azure IoT Edge supports Debian Bullseye on ARM32v7](https://azure.microsoft.com/en-us/updates/azure-iot-edge-supports-debian-bullseye-arm32v7/) )

### Retirement
* Debian 9 (Stretch) for ARMHF ( [Update your IoT Edge devices on Raspberry Pi OS Stretch](https://azure.microsoft.com/en-us/updates/update-rpios-stretch-to-latest/) )

### Compatibility script (Under development)
The IoT Edge compatibility script performs a variety of checks to determine whether a platform has the necessary capabilities to run IoT Edge. This stand-alone script is still considered under development, but we invite anyone to give it a try and send us your feedback by posting in the Issues. Go [here ](https://github.com/Azure/iotedge/blob/main/platform-validation/docs/aziot-compatibility-get-started.md) to learn more about the checks it performs and how to use it.

### Known issue: Debian 10 (Buster) on ARMv7
We recommend using Bullseye instead of Buster as the host OS.  Seccomp on Buster may not be aware of new system calls used by your container resulting in crashes.

If you need to use Buster, then apply the following workaround to change the default seccomp profile for Moby's `defaultAction` to `SCMP_ACT_TRACE`:
1. Make sure you are runing latest docker and latest seccomp package from oldstable channel
2. Download [Moby's default seccomp profile](https://github.com/moby/moby/blob/master/profiles/seccomp/default.json) and put it somewhere. 
4. On line 2 change the value for _defaultAction_ from `SCMP_ACT_ERRNO` to `SCMP_ACT_TRACE`
5. Edit file _/etc/systemd/system/multi-user.target.wants/docker.service_ to have it contain: `--seccomp-profile=/path/to/default.json`
6. Restart your container engine by running:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl restart docker
   ```


## Edge Agent
* Remove unused plan runner and planner  ( [2159dfad3](https://github.com/Azure/iotedge/commit/2159dfad36f04c61ed1df6df4afd69ea57439650) )
* Flatten additional properties of metrics  ( [dbc6af347](https://github.com/Azure/iotedge/commit/dbc6af347adef00a3091be7ae188a1c75fb58181) )
* Update Device SDK to the latest LTS version  ( [90e5b3264](https://github.com/Azure/iotedge/commit/90e5b3264ac0befe1eeebce898f01635f4ac7d14) )
* Update ARM32 and ARM64 images to use Alpine  ( [059aaea2d](https://github.com/Azure/iotedge/commit/059aaea2d23d11bfb5b46dac3b28f9a563395647) )
* Migrate to Dotnet 6  ( [37234e02b](https://github.com/Azure/iotedge/commit/37234e02b500e6930d389275ac09a5aee80f7445) )
* Device product information  ( [9faf5a5c0](https://github.com/Azure/iotedge/commit/9faf5a5c09fd3ef058201075f813da2a0a81cdd6) )
* Update references to the default branch  ( [04ee9751f](https://github.com/Azure/iotedge/commit/04ee9751f08691b1bff157829a1498fc71998ab5) )
* Update Microsoft.Azure.Devices.Client from 1.36.3 to 1.36.4  ( [19beaae55](https://github.com/Azure/iotedge/commit/19beaae556897a2b0676c455523df136ea016e73) )
* Remove k8s projects from master  ( [d81a032bc](https://github.com/Azure/iotedge/commit/d81a032bcb740bccccff3a13c809547d2ca62362) )
* Fix underflow possibility on ColumnFamilyDbStore  ( [bc78f1c](https://github.com/Azure/iotedge/commit/bc78f1c0b0ab0dfa305b817c030b82cea035f6e0) )
* Remove BouncyCastle dependency  ( [aa2237988](https://github.com/Azure/iotedge/commit/aa22379885dff7cbb89cbdf1e4c870460f6f5576) )
* Fix Workload socket issue for concurrent module create  ( [26bbf7145](https://github.com/Azure/iotedge/commit/26bbf7145b1cd76fd55cb7b7d990a3907616ac5a) )
* Handle Return Code From Get Module Logs Failure  ( [5015eca6d](https://github.com/Azure/iotedge/commit/5015eca6d497e317a28ed0dc3bc013315c34b53b) )
* Update SDK from 1.36.2 to 1.36.3 to fix connectivity issues  ( [865b275b4](https://github.com/Azure/iotedge/commit/865b275b4703cd6c41c2f63623994b133d3e3827) )
* Restrict EdgeAgent parallel calls to edged to 5   ( [3bb4c8f7f](https://github.com/Azure/iotedge/commit/3bb4c8f7fc2bb90a5db6c26d36d16f8fbc3b3c50) )
* Recreate edgeAgent when not `Running`, `Stopped`, or `Failed`  ( [6b21874fe](https://github.com/Azure/iotedge/commit/6b21874fee75ef49d497b5f635048e34a7f2ca9f) )
* Add `RocksDB_MaxManifestFileSize` env var  ( [2c878635c](https://github.com/Azure/iotedge/commit/2c878635c75d3eaddd55c06ae5fd9229d9fe463b) )
* Update SDK references to fix `Dotnetty` bug  ( [0750a4414](https://github.com/Azure/iotedge/commit/0750a44146c81ea3cce9bc447016b535612502fc) )
* Update k8s client  ( [edad631d7](https://github.com/Azure/iotedge/commit/edad631d7d2d3b0043b59e652577055efb30d783) )
* Fix edgeAgent creates rogue `ModuleClients` in case of error  ( [e3892eb4a](https://github.com/Azure/iotedge/commit/e3892eb4aae8b59bf9f5be02a34af693f9f23dc9) )
* Fix various RUSTSEC  ( [89917f1bb](https://github.com/Azure/iotedge/commit/89917f1bb59fe98c9872c16aea5cab855fa6e139) )
* Make sure to dispose `HttpContentStream` when done reading module logs.  ( [43d662397](https://github.com/Azure/iotedge/commit/43d6623971cdda453047696ec364e8fe0e511935) )
* Introduce multiple workload sockets  ( [323bdc9ac](https://github.com/Azure/iotedge/commit/323bdc9acd43a091b78d9e616d640cbf7bfb8422) )
* Fix delayed frequent twin pulls on reconnect  ( [c87e85b0f](https://github.com/Azure/iotedge/commit/c87e85b0fa59eb7323cbebb8bacccd1181bc39f3) )
* Properly dispose UDS for Workload Client. ( [472cee5](https://github.com/Azure/iotedge/commit/472cee580101cfd5999492ef3760d5038679680e), [f9cdb59](https://github.com/Azure/iotedge/commit/f9cdb5902a8e13b80bbd8040323fb0729f085dc3) )
* Use Docker Timestamp When Log Timestamp is not Available in JSON log  ( [00cfb6fbe](https://github.com/Azure/iotedge/commit/00cfb6fbe323f43a6629ecf531beb13ef6c556fe) )
* Don't dispose stream too early  ( [ce0ca9a87](https://github.com/Azure/iotedge/commit/ce0ca9a8741e565601ff67a65fb2288bd3cbbfae) )
* Change default uid  ( [b443b0c2f](https://github.com/Azure/iotedge/commit/b443b0c2f130bbc18060dff9cd2dca26ca6e014e) )
* Update `GetModuleLogs` method when `tail + since + until` options are provided.  ( [32df5ee8a](https://github.com/Azure/iotedge/commit/32df5ee8adc829dc697efea7382f110994f546c1) )
* `$upstream` support for container registry address  ( [58f5faa0c](https://github.com/Azure/iotedge/commit/58f5faa0ca2d4368c747c4ef25ee276a7cac68a5) )
* Resolve security concern in logging  ( [e96554c63](https://github.com/Azure/iotedge/commit/e96554c632221892c7bb4b5f79cb13cca0ba4902) )
* Verify Twin Signatures  ( [e8d2bc270](https://github.com/Azure/iotedge/commit/e8d2bc2705adc65c91a821a6d934fbe0d76e3291) )


## Edge Hub
* Remove experimental mqtt broker code  ( [85084e4f0](https://github.com/Azure/iotedge/commit/85084e4f04aafbd7b68931e80d3c84f28eb47585) )
* Batch incoming amqp messages to optimize sender feedback  ( [5667c58ce](https://github.com/Azure/iotedge/commit/5667c58ce0a70f47026efa87fabf29b3ef92c9c1) )
* Bump Device SDK to latest LTS version  ( [90e5b3264](https://github.com/Azure/iotedge/commit/90e5b3264ac0befe1eeebce898f01635f4ac7d14) )
* Restrict TLS protocol to 1.2 for EdgeHub and ApiProxy modules  ( [4a76a20b1](https://github.com/Azure/iotedge/commit/4a76a20b142fd59e6bb44110e1ecd6e6519fc1d7) )
* Update agent ARM32/64 images to use Alpine  ( [059aaea2d](https://github.com/Azure/iotedge/commit/059aaea2d23d11bfb5b46dac3b28f9a563395647) )
* Configurable task for cancelling upstream calls  ( [cf9e04987](https://github.com/Azure/iotedge/commit/cf9e049874c72ea86ee804d2f1b57132da421c45) )
* Build docker images with embedded metadata  ( [a458af376](https://github.com/Azure/iotedge/commit/a458af376177adacb25349cc8f59df8aae9e1a15) )
* Migrate to Dotnet 6  ( [37234e02b](https://github.com/Azure/iotedge/commit/37234e02b500e6930d389275ac09a5aee80f7445) )
* Rust toolchain upgrade fixes  ( [a45cc5f71](https://github.com/Azure/iotedge/commit/a45cc5f71e200e78e4e93ae41e2731244bb20ac9) )
* Device product information  ( [9faf5a5c0](https://github.com/Azure/iotedge/commit/9faf5a5c09fd3ef058201075f813da2a0a81cdd6) )
* Update `regex` to 1.5.5  ( [9f0f7f424](https://github.com/Azure/iotedge/commit/9f0f7f42472f658893ff876ad730338ab9833590) )
* Upgrade Rust toolchain  ( [ab700e82a](https://github.com/Azure/iotedge/commit/ab700e82ad6ca15140832fbb9970075e2e331073) )
* Update Microsoft.Azure.Devices.Client from 1.36.3 to 1.36.4  ( [19beaae55](https://github.com/Azure/iotedge/commit/19beaae556897a2b0676c455523df136ea016e73) )
* Remove `thread_local` for non-edgelet projects  ( [6db976def](https://github.com/Azure/iotedge/commit/6db976def4930a6a5a0f5630170ea4d3d6f8f1b7) )
* Add more logging to certificate import  ( [49d41df98](https://github.com/Azure/iotedge/commit/49d41df98fbd3abe246a582a9169a8e7d137068d) )
* Fix edgeHub shutdown for renew certificate  ( [fcd4d007a](https://github.com/Azure/iotedge/commit/fcd4d007acad0a746916da0b7f6bc933c4b6e641) )
* AMQP CBS token message dispose  ( [4179221bc](https://github.com/Azure/iotedge/commit/4179221bcc9596388386e3faca87cc28bc6af8f2) )
* Fix underflow possibility on ColumnFamilyDbStore  ( [bc78f1c](https://github.com/Azure/iotedge/commit/bc78f1c0b0ab0dfa305b817c030b82cea035f6e0) )
* Remove `BouncyCastle` dependency  ( [aa2237988](https://github.com/Azure/iotedge/commit/aa22379885dff7cbb89cbdf1e4c870460f6f5576) )
* Update Base Images for a Security Patch  ( [e6d52d6f6](https://github.com/Azure/iotedge/commit/e6d52d6f6b0eb76e7ef250f3fcdeaf38e467ab4f), [7e0c1a5d3](https://github.com/Azure/iotedge/commit/7e0c1a5d38755a04ee0b802723a9474cd50eec87), [704250b04](https://github.com/Azure/iotedge/commit/704250b041be242938eb8a238513977a5b452600), [b592e4776](https://github.com/Azure/iotedge/commit/b592e47760d0b071c18a13c54de9125d990abcfd), [5cb16fb5d](https://github.com/Azure/iotedge/commit/5cb16fb5d8f2ad8f1b7df4092c29333af837df9b), [b00a78805](https://github.com/Azure/iotedge/commit/b00a78805384deff1ce2c252302a260f31c31faa) )
* Allow identity translation for subscriptions  ( [5fbd0d9f3](https://github.com/Azure/iotedge/commit/5fbd0d9f30fe9fea0f2fcc2a2e7586068131ef5e) )
* Update vulnerable `nix` version  ( [33c8a778f](https://github.com/Azure/iotedge/commit/33c8a778fec079a0045c9abadb832824b39368bd) )
* Wait for configuration before starting protocol heads  ( [b6c5d861b](https://github.com/Azure/iotedge/commit/b6c5d861b027be9910cbdf5a9e92d026aac2c71d) )
* Update dependency on vulnerable package  ( [76c22bf10](https://github.com/Azure/iotedge/commit/76c22bf1031b3ab99f0dacc99c9d6779ec466225) )
* Update SDK from 1.36.2 to 1.36.3 to fix connectivity issues  ( [865b275b4](https://github.com/Azure/iotedge/commit/865b275b4703cd6c41c2f63623994b133d3e3827) )
* Fix `edgehub_queue_len` counting  ( [d3f649886](https://github.com/Azure/iotedge/commit/d3f6498860911cbbee2f3f71bf91b821c9a89e01) )
* Fix detect fail-over from iot hub/sdk behavior and disconnect from hub  ( [676a0f58c](https://github.com/Azure/iotedge/commit/676a0f58c051a72bbe39753b35218b9ad4275f1b) )
* Remove WebSocket Ping KeepAlives  ( [31531ec22](https://github.com/Azure/iotedge/commit/31531ec22f72e3df62fa50d4995f15352508d180) )
* Update links to docs from .md files  ( [97c803071](https://github.com/Azure/iotedge/commit/97c803071a9ace2b7fd22992233496aca9bfe1f6) )
* Fix `OnReconnectionClientsGetTwinsPulled()` increased timeout  ( [e6ddd546b](https://github.com/Azure/iotedge/commit/e6ddd546b359516d7917d48eee88b6efc34073b0) )
* Add `RocksDB_MaxManifestFileSize` env var  ( [2c878635c](https://github.com/Azure/iotedge/commit/2c878635c75d3eaddd55c06ae5fd9229d9fe463b) )
* Add connection-check for direct method test  ( [0ad320041](https://github.com/Azure/iotedge/commit/0ad32004144dfc73fe8de513364b5b5e046f775b) )
* Update SDK references to fix Dotnetty bug  ( [0750a4414](https://github.com/Azure/iotedge/commit/0750a44146c81ea3cce9bc447016b535612502fc) )
* Create identities for leaf  ( [ca2f4aac5](https://github.com/Azure/iotedge/commit/ca2f4aac5ba062474348c54d6e5ef963d9c41a73) )
* Add `ComponentName` to message properties  ( [9a32670dd](https://github.com/Azure/iotedge/commit/9a32670dd04ff24a15def922b0ac7a65d465cb02) )
* Remove redundant tests and wait for device to be disconnected  ( [221048a9c](https://github.com/Azure/iotedge/commit/221048a9c4278338077f406bd785438b4e174099) )
* Fix exception type in `BrokerConnection::SendAsync`  ( [bbe3525af](https://github.com/Azure/iotedge/commit/bbe3525af33b99f0480cc289afd6f90ac0b7367b) )
* Don't dispose stream too early  ( [ce0ca9a87](https://github.com/Azure/iotedge/commit/ce0ca9a8741e565601ff67a65fb2288bd3cbbfae) )
* Fix edgeHub error code propagation in case of an error   ( [8250d87a5](https://github.com/Azure/iotedge/commit/8250d87a558fd0ed096f570589be2bf7955af5a5) )
* Change default uid  ( [b443b0c2f](https://github.com/Azure/iotedge/commit/b443b0c2f130bbc18060dff9cd2dca26ca6e014e) )
* Format error message in registry controller (#4776)  ( [0dceddcfa](https://github.com/Azure/iotedge/commit/0dceddcfad2f2f11148265e3ee5f1f3c03d1190b) )
* Fix `edgehub_queue_len_metric`  ( [065bf3297](https://github.com/Azure/iotedge/commit/065bf32973fba25f89327b8756308593d778b3b7) )
* Update rust toolchain to 1.52.1  ( [e5218d1e7](https://github.com/Azure/iotedge/commit/e5218d1e70ce71929cc1cabf2965e7a196ff9614) )
* Overwrite `IsDirectConnection` flag when device changes from `Indirect`  ( [68d5ebff4](https://github.com/Azure/iotedge/commit/68d5ebff4144a47b4550c4035d22bf21db01c4c7) )
* Restore device scopes from older store (version < 1.2)  ( [207a5f07b](https://github.com/Azure/iotedge/commit/207a5f07babcb40f71b466f75f0b42db09582b15) )
* Upgrade cargo deps for watchdog  ( [797df90bc](https://github.com/Azure/iotedge/commit/797df90bcddcd7417ee3010f4215ed8f90bae39d) )
* Close AMQP connection explicitly when no more links  ( [6c8134e6c](https://github.com/Azure/iotedge/commit/6c8134e6c7cc7674f27955e646a566df38ac1adc) )
* Add `SharedAccessSignature` to repo with fix for vulnerability  ( [6c4269a0b](https://github.com/Azure/iotedge/commit/6c4269a0bc7640e22aa60461c2109fbf96f95ef3) )
* Add validation for null props inside objects inside arrays.  ( [f96961f4a](https://github.com/Azure/iotedge/commit/f96961f4a078dff6733eb88311de442613a70d77) )
* Fix resolving BrokeredCloudProxyDispatcher   ( [ef27142f9](https://github.com/Azure/iotedge/commit/ef27142f98359b5883813ef172fbb637f4345144) )
* Fix getDeviceAndModuleOnBehalfOf to check if target device is in scope  ( [7c3261a67](https://github.com/Azure/iotedge/commit/7c3261a67fbdf46b1b08c450a5b99e26734a5600) )
* Send connection device Id information on twin change notifications  ( [cd39064f5](https://github.com/Azure/iotedge/commit/cd39064f5291f04c0b1606b81573b67b2f5a2dc6) )
* Update `HttpClient` timeout for scope sync  ( [5b22e774f](https://github.com/Azure/iotedge/commit/5b22e774f67579072f45314f40e75d376d5d10b8) )
* Add caching to TokenProvider ( [8988456](https://github.com/Azure/iotedge/commit/8988456377154de075e83d6a896778d7200a1a61) )
* Registry API On-behalf-of calls auth check fix  ( [cad6c5b0c](https://github.com/Azure/iotedge/commit/cad6c5b0c726d23c1d5187667f94c443676c3ebb) )
* Device scope cache refresh  ( [44b599caa](https://github.com/Azure/iotedge/commit/44b599caaa54445c2d25b8f9496073319c1cd452) )
* Update rust toolchain to 1.51  ( [0f1d90c7c](https://github.com/Azure/iotedge/commit/0f1d90c7c7cec47a087f749da8245c9f6e84a165) )
* Update bridge config validation.  ( [78236a7ba](https://github.com/Azure/iotedge/commit/78236a7bad08bf98585f602f01030d47d66de141) )
* Add edgeHub identity to the scopes cache at the startup  ( [0dbdd0577](https://github.com/Azure/iotedge/commit/0dbdd0577c351083697be8b7ee760c65b9d9c5c8) )
* Drop messages when device is not in scope and auth mode is Scope  ( [51ad827de](https://github.com/Azure/iotedge/commit/51ad827de4106bc5d68889cc502c434d1d01aa42) )
* Update client twins after disconnect/connect  ( [794c32459](https://github.com/Azure/iotedge/commit/794c32459051e1a9d4ea079e3a28484649081a4b) )
* Throw transient error when edgeHubCore is disconnected from the broker  ( [b196a15e3](https://github.com/Azure/iotedge/commit/b196a15e3dd3fb541d120f4f947c6557c518ed5c) )
* Don't unsubscribe when there is no subscription registered  ( [53ff15b8c](https://github.com/Azure/iotedge/commit/53ff15b8ce1c619e2bf32da63ab9eba0c718d00f) )
* CloudConnection did not forward `close()` call to cloud proxy  ( [6f3f8ecc4](https://github.com/Azure/iotedge/commit/6f3f8ecc4e8caced6424136076d6092bdabbf759) )
* Move `NestedEdgeEnabled` out of experimental features. (#4467)  ( [7e0fc1fae](https://github.com/Azure/iotedge/commit/7e0fc1fae2e441bc7f451b3582f545b44c84cc37) )
* Add a separate message pump for messages from upstream  ( [0e6985445](https://github.com/Azure/iotedge/commit/0e6985445110952015fa48681e1d620183ed6279) )
* Verify Twin Signatures  ( [e8d2bc270](https://github.com/Azure/iotedge/commit/e8d2bc2705adc65c91a821a6d934fbe0d76e3291) )


## aziot-edge
* Enable Edge CA auto-renewal by default ( [04bd75d9c](https://github.com/Azure/iotedge/commit/04bd75d9c5e779603549ea070ce72b6def4dbc05) )
* Correct handling of `/images/create` response stream  ( [287629d09](https://github.com/Azure/iotedge/commit/287629d09e5265736c0374ce406566fa959ce5f8) )
* Fix debug artifacts being used in the release pipeline.  ( [59b192cff](https://github.com/Azure/iotedge/commit/59b192cff8427dd112ceed2709b37cf7a9421380) )
* Flatten additional properties of metrics  ( [dbc6af347](https://github.com/Azure/iotedge/commit/dbc6af347adef00a3091be7ae188a1c75fb58181) )
* Upgrade to latest Rust version  ( [9f674bdf5](https://github.com/Azure/iotedge/commit/9f674bdf5e47b21d8ad24ee983f7259fd9379bd8), [f9c174f98](https://github.com/Azure/iotedge/commit/f9c174f98ec4433c4f5ad4716b27df8cb30dc993), [4dfe8b1bf](https://github.com/Azure/iotedge/commit/4dfe8b1bf0fa5bdc341f6b8e01ce8458537d9ad7) )
* Remove `check_submodules` tool  ( [038f1c5a2](https://github.com/Azure/iotedge/commit/038f1c5a232d8c015a5814bcfb99271102879ed5) )
* Fix for new hostname conflicting with old modules  ( [bb844b5a8](https://github.com/Azure/iotedge/commit/bb844b5a8d7f132c003590296e68647cf315faec) )
* Fix exit code when restarting due to reprovision  ( [223f3922a](https://github.com/Azure/iotedge/commit/223f3922a7b923f2d3e1e228ea8a2b204fb30f4c) )
* Fix subject name setting of Edge CA  ( [921840e02](https://github.com/Azure/iotedge/commit/921840e027f1e8f2d6635f75230e4616bab2e468) )
* Remove Debian 9 from main  ( [30a1ee5d9](https://github.com/Azure/iotedge/commit/30a1ee5d92359a84b2469a691e41180bf56471a4) )
* Build docker images with embedded metadata  ( [a458af376](https://github.com/Azure/iotedge/commit/a458af376177adacb25349cc8f59df8aae9e1a15) )
* Add auto-renewal of the Edge CA cert  ( [d8ae9bd7d](https://github.com/Azure/iotedge/commit/d8ae9bd7d4efc0f355ecf9c8d99d5f4b7b606c73) )
* RHEL8 packages  ( [53d3afc2a](https://github.com/Azure/iotedge/commit/53d3afc2aab4b300a832060050b1b3d5d41200df) )
* Add settings for auto-renewal of Edge CA  ( [a8fb6465e](https://github.com/Azure/iotedge/commit/a8fb6465ea1f0cb23b22533bbefa7117aa54b71e) )
* Remove `failure` dependency  ( [496c89924](https://github.com/Azure/iotedge/commit/496c89924e53b5225b0d5cff813b05e3acb10dae) )
* Device product information  ( [9faf5a5c0](https://github.com/Azure/iotedge/commit/9faf5a5c09fd3ef058201075f813da2a0a81cdd6) )
* Upgrade Rust toolchain  ( [bf3f444b8](https://github.com/Azure/iotedge/commit/bf3f444b82038b762fa09058b45c6ae7bd1b9b2f) )
* Update regex to 1.5.5  ( [9f0f7f424](https://github.com/Azure/iotedge/commit/9f0f7f42472f658893ff876ad730338ab9833590) )
* Update scripts for removing keys and certificates on edge device  ( [9557aecff](https://github.com/Azure/iotedge/commit/9557aecffc6fdeec0fa6ed7705ca947c95b61136) )
* Update references to the default branch  ( [04ee9751f](https://github.com/Azure/iotedge/commit/04ee9751f08691b1bff157829a1498fc71998ab5) )
* Update tokio, rayon, and crossbeam to latest compatible versions  ( [54163699b](https://github.com/Azure/iotedge/commit/54163699b0db1b1c8eb3ff5b7ab15fd5f6137857) )
* Upgrade Rust toolchain  ( [ab700e82a](https://github.com/Azure/iotedge/commit/ab700e82ad6ca15140832fbb9970075e2e331073) )
* Move test clients and functions to iot-identity-service  ( [f8155c06a](https://github.com/Azure/iotedge/commit/f8155c06acc86c12ecdac386eb95cccb07487d82) )
* Update cargo dependency  ( [512f1364b](https://github.com/Azure/iotedge/commit/512f1364b40d8b0d9fe2102d459696f7cc1538d5) )
* Add Instructions to Run Azure IoT Edge Daemon Locally  ( [bd43e5d5e](https://github.com/Azure/iotedge/commit/bd43e5d5ebbf64c373f5849feff9bb8c644bc04c) )
* Update vulnerable `regex` package  ( [cfeea7d14](https://github.com/Azure/iotedge/commit/cfeea7d14762629a7b7d2d69f6fd755ed9e3edb7) )
* Change default common name of Edge CA cert to "aziot-edge CA"  ( [a62e2cad6](https://github.com/Azure/iotedge/commit/a62e2cad6e42d3a97dabfc37c12cb3ac52ef1ecb) )
* Update vulnerable `nix` version  ( [33c8a778f](https://github.com/Azure/iotedge/commit/33c8a778fec079a0045c9abadb832824b39368bd) )
* Update tokio to 1.15.0  ( [c941f0605](https://github.com/Azure/iotedge/commit/c941f06055e827e75c606cd02dab37aebd4b87ed) )
* Update edgelet cargo dependency  ( [132e1d340](https://github.com/Azure/iotedge/commit/132e1d34037a6c2332d9e9734c703cf3fa86253b) )
* Iotedge check proxy-settings  ( [dc6d0d093](https://github.com/Azure/iotedge/commit/dc6d0d093f88439fce97f3115f003a0f50dde9db) )
* Remove moby check  ( [3b95ec7c9](https://github.com/Azure/iotedge/commit/3b95ec7c9a9b6a7226828e6628adfa4fb963be6f) )
* Remove Subject Alternate Name Sanitization in Workload Cert Creation  ( [070610dbc](https://github.com/Azure/iotedge/commit/070610dbc54ec9078e149dcd8457e4c5a54b2280) )
* Reorder `identity_pk` and `identity_cert`  ( [cb3d8b552](https://github.com/Azure/iotedge/commit/cb3d8b552a9a8d1e546853c8b430c3dbccda07f3) )
* Fix typo in template configuration  ( [02cf5a733](https://github.com/Azure/iotedge/commit/02cf5a7335aa20cc1f4b919df266e6db90b33534) )
* Update template configuration with subject DN options  ( [452fcc5ee](https://github.com/Azure/iotedge/commit/452fcc5ee588d554108a1af329e529365efa8db8) )
* Fix bug where Edge CA is always self-signed  ( [4e7a5bbab](https://github.com/Azure/iotedge/commit/4e7a5bbab9c25f38af2a1825be874df1ac7aaa13) )
* Use IS client retries  ( [87f978e4f](https://github.com/Azure/iotedge/commit/87f978e4f0decd26471f3ab27aab1ab7495e915e) )
* Recreate edgeAgent when not Running, Stopped, or Failed  ( [6b21874fe](https://github.com/Azure/iotedge/commit/6b21874fee75ef49d497b5f635048e34a7f2ca9f) )
* Expand build targets to include Debian11  ( [a9dc1df65](https://github.com/Azure/iotedge/commit/a9dc1df65f08f7a8aa533f6d50af2f3be7a96058) )
* Update cargo dependency  ( [31c4afa17](https://github.com/Azure/iotedge/commit/31c4afa176ef1b9aa68ab1f4e15c2d360a89d203) )
* Add doc for device ID and Edge CA certs over EST  ( [1d58e64c3](https://github.com/Azure/iotedge/commit/1d58e64c375290fa588e1351e9ceedf89872a505) )
* Fix missing uptime in iotedge list  ( [f0cb947ab](https://github.com/Azure/iotedge/commit/f0cb947ab4394e9374bbd2ea48b9a496a20fe762) )
* Fix aziot-edged startup when mnt is missing  ( [68f564c77](https://github.com/Azure/iotedge/commit/68f564c7763a136340ebccaf169ab3f669149fa2) )
* Disable connection pooling for docker client.  ( [b35d36493](https://github.com/Azure/iotedge/commit/b35d36493bc011c5b70729ca095974d396f2a9ad) )
* Renew Edge CA on startup of edged  ( [96d003115](https://github.com/Azure/iotedge/commit/96d0031155d35869f997c75ef0007a066750a057) )
* Use 1ES hosted agent for amd64 single-node connectivty tests  ( [b4b2d7d93](https://github.com/Azure/iotedge/commit/b4b2d7d93ba09a6d3fd382575b64124cfc821c5a) )
* Update edgelet to use tokio 1  ( [4c2f173b3](https://github.com/Azure/iotedge/commit/4c2f173b30025c77e7939b48a968c9a50f269559) )
* Fix various RUSTSEC  ( [89917f1bb](https://github.com/Azure/iotedge/commit/89917f1bb59fe98c9872c16aea5cab855fa6e139) )
* Add timestamp to the default support-bundle filename  ( [d7f36c178](https://github.com/Azure/iotedge/commit/d7f36c17894ca3ca4fcc5add52ca9a6abe68c494) )
* Handle `proxy_uri` consistently in iotedge check  ( [ff79848aa](https://github.com/Azure/iotedge/commit/ff79848aa2d5c453cbe0f10022ce0b94e6737a80) )
* Fix host cpu metric incorrectly reported at 100% (#5204)  ( [3eaaae993](https://github.com/Azure/iotedge/commit/3eaaae993b021065dcefb907a724e6a59f02587b) )
* Implement throttling mechanism to prevent spamming of workload socket  ( [63c566b97](https://github.com/Azure/iotedge/commit/63c566b977c45f3f55b6d9ed24ca29acd9ab96b2) )
* Update connectivity check on ports to skip checks when not needed  ( [ec491d799](https://github.com/Azure/iotedge/commit/ec491d799711300d098c31b63ce86aba2570aa3c) )
* Introduce multiple workload sockets  ( [323bdc9ac](https://github.com/Azure/iotedge/commit/323bdc9acd43a091b78d9e616d640cbf7bfb8422) )
* Fix Privileged Flag  ( [07d6c3c67](https://github.com/Azure/iotedge/commit/07d6c3c67c4c72e7c71426d8c88ed0538c64418b) )
* Introduce `Timestamps` Option via mgmt.sock (#4970)  ( [244723e5c](https://github.com/Azure/iotedge/commit/244723e5c4e0062c0f8fc47af0e9cd8f161cd49c) )
* Improve log message for container state  ( [c07ade738](https://github.com/Azure/iotedge/commit/c07ade7389c5d5eca2cc3650b5be3e904faf4f53) )
* Device config has `allow_privileged` flag  ( [6a035ea09](https://github.com/Azure/iotedge/commit/6a035ea0937883a5e57942bbed33ba7c5d8227e5) )
* Fix DPS E2E tests  ( [46db9fdfc](https://github.com/Azure/iotedge/commit/46db9fdfc2ef45666dfa4d20c588f7361c4afe6e) )
* Enable aziot-edged in CentOS package  ( [dafe2ece2](https://github.com/Azure/iotedge/commit/dafe2ece2b7cd195a8375fbff7df2e792d04c937) )
* Limit sysinfo crate FDs usage.  ( [bc5606131](https://github.com/Azure/iotedge/commit/bc56061318b9f2df8e30ab51a22bd8e63daa3d01) )
* Change default uid  ( [b443b0c2f](https://github.com/Azure/iotedge/commit/b443b0c2f130bbc18060dff9cd2dca26ca6e014e) )
* Make edgelet uses `humantime` instead of `parse_duration`  ( [450830433](https://github.com/Azure/iotedge/commit/450830433c506ab50cd9c90bcea4b7dab421a1e3) )
* Edgelet RUSTSEC dep update  ( [6cae62e46](https://github.com/Azure/iotedge/commit/6cae62e46e2a435f1622ab8c774e944e1472722d) )
* `$upstream` support for container registry address  ( [58f5faa0c](https://github.com/Azure/iotedge/commit/58f5faa0ca2d4368c747c4ef25ee276a7cac68a5) )
* Registration ID is optional in super-config  ( [35da91ee8](https://github.com/Azure/iotedge/commit/35da91ee8c330d8c32d3123fdb806b4b96dbd76f) )
* Fix auth certs for EST-issued Edge CA in `iotedge config apply` ( [4e29eabc8](https://github.com/Azure/iotedge/commit/4e29eabc83ca9e7d2ee5d96b2d3c7364f2f7dc54) )
* Fix Edge CA and module cert CSRs to use version 0 (v1) instead of non-existent version 2 (v3).  ( [a88f820a5](https://github.com/Azure/iotedge/commit/a88f820a5c26b617661d8c3ef33b8dda37add866) )
* Support issued Edge CA cert in `iotedge config apply`  ( [0d579a75f](https://github.com/Azure/iotedge/commit/0d579a75f4cadec775b6e715112ef238fe449308) )
* Resolve security concern in logging  ( [e96554c63](https://github.com/Azure/iotedge/commit/e96554c632221892c7bb4b5f79cb13cca0ba4902) )
* Validate connection string during `iotedge config mp`  ( [10c82de0d](https://github.com/Azure/iotedge/commit/10c82de0df94bf335fabd06581fc1fbe5bc5aac5) )
* Update iot-identity-service dependency  ( [d7cc38c27](https://github.com/Azure/iotedge/commit/d7cc38c27b3562e4c81cb3fd1602adfeb5f1257a), [5c423cf87](https://github.com/Azure/iotedge/commit/5c423cf8752558bc4b3699ff35c26fb6a10415eb) )
* Update the dev version to 1.2 ( [1a796160e](https://github.com/Azure/iotedge/commit/1a796160e74eeb6f13d86373bbe7976e29ae8581) )
* Fix for expired CA certificate not renewing  ( [04e78bd85](https://github.com/Azure/iotedge/commit/04e78bd850815303c613fd19292e6c1b231fec31) )
* Make super config public  ( [825017957](https://github.com/Azure/iotedge/commit/825017957c91ed407b8c6d2eec70e6943a32749d) )
* Fix links in help message  ( [8533efe2c](https://github.com/Azure/iotedge/commit/8533efe2c829db5623cb64dc09749489de803e28) )
* `aziotctl system` improvements + `system status` formatting changes  ( [e9923a619](https://github.com/Azure/iotedge/commit/e9923a61978972c7c2247aaadf1ba2d8312bfda7) )
* Add iotedge user to systemd-journal group  ( [1ec948635](https://github.com/Azure/iotedge/commit/1ec948635964d1fa50cd49d33fb97512f36665d3) )
* Update cargo dependency for iot-identity-service  ( [8a6b87fca](https://github.com/Azure/iotedge/commit/8a6b87fcad42a4aad89b010abfacec84b6e9d459) )
* Update iotedge check for version 1.2.0  ( [80f95d83a](https://github.com/Azure/iotedge/commit/80f95d83afb853a4977569a05bc845809411e2b8) )
* Remove references to 'iotedged' from `iotedge` help text  ( [0f82c622b](https://github.com/Azure/iotedge/commit/0f82c622b345e1f454b185665ae1a0f38a101790) )
* Cache device provisioning state  ( [d9be1e994](https://github.com/Azure/iotedge/commit/d9be1e994cf556c27d991bf34cc8f9ad9754a9ea) )
* Fix check-agent-image-version check for nested Edge scenarios.  ( [146f53052](https://github.com/Azure/iotedge/commit/146f530520eed20aae4103f4b168d1db0aff1b1b) )
* Document the super-config's `agent.config.createOptions` value format more clearly.  ( [28ec7b56a](https://github.com/Azure/iotedge/commit/28ec7b56a8298a99b7710a689290a7bfd477d793) )
* Prepend iotedge-config suggestions with sudo.  ( [e021231b3](https://github.com/Azure/iotedge/commit/e021231b3d30080204dbf12135cd5d5faca01ec8) )
* Import master encryption key in `iotedge config import`  ( [1b2ece4a0](https://github.com/Azure/iotedge/commit/1b2ece4a0b9f535db45c09b9ddabc661436ef071) )
* Fix `iotedge config apply` not picking up parent hostname because of serde bug.  ( [fb3c42c80](https://github.com/Azure/iotedge/commit/fb3c42c8059d0640c31c04256169d7df9e54f69c) )
* Fix self-signed edge-ca cert to use its subject name as the issuer name.  ( [40ddfff90](https://github.com/Azure/iotedge/commit/40ddfff90505d1fb5b5b9222dc0f89882b0b9765) )
* Set default agent version to 1.2.0-rc4  ( [d7ad36670](https://github.com/Azure/iotedge/commit/d7ad36670f711eae30a0b920d9952a35e8303e21) )
* Read `parent_hostname` configuration from aziot  ( [13124b87c](https://github.com/Azure/iotedge/commit/13124b87c5081d08227630af803fc5798f563af1) )
* Iotedge system stop  ( [94226fd1c](https://github.com/Azure/iotedge/commit/94226fd1c4698f21f25acd92abd58152d95a47b8) )
* Remove leftover unused lint exceptions  ( [9d43de593](https://github.com/Azure/iotedge/commit/9d43de59301abfd187a5db80a34b830e0ea4ae4b) )
* Use unique common name for edged-ca cert when apply'ing super-config.  ( [34e7a6c72](https://github.com/Azure/iotedge/commit/34e7a6c7253c7091a9949cdccbfee93fad5761fa) )
* Bump `serde-yaml` version to 0.8  ( [226c01b51](https://github.com/Azure/iotedge/commit/226c01b5182d401c13259a150f47aa5aa3d2d714) )
* Change default quickstart Edge CA expiry to 90 days.  ( [0a1c70406](https://github.com/Azure/iotedge/commit/0a1c70406adc2f5fbdcb4eec6195318d039f0265) )
* Re-add dynamic provisioning support  ( [c0997a78f](https://github.com/Azure/iotedge/commit/c0997a78ff31c542773dd9b79b7accabde7e8ad4) )
* Add iotedge system reprovision  ( [98c916839](https://github.com/Azure/iotedge/commit/98c91683986595d4430f1770eaffe61ac5ce5ebd) )
* Fix versioning scheme  ( [9737395cf](https://github.com/Azure/iotedge/commit/9737395cf3761fb7642357229c1a7099b2d9e28c) )
* Add check version for agent image  ( [deb8a62b8](https://github.com/Azure/iotedge/commit/deb8a62b888a60d7fd62b06c1933e44075c2cef2) )
* `iotedge check` improvements for nested edge  ( [22819dd7f](https://github.com/Azure/iotedge/commit/22819dd7fba0567a7273e3c3219630ccfef5ce50) )
* Add "required" annotation to iotedge-config-mp's `--connection-string` parameter.  ( [102936097](https://github.com/Azure/iotedge/commit/102936097b6dee2d630ea2c8c550ec65564d9b71) )
* Remove constrain that makes no sense in general case  ( [168a79c2b](https://github.com/Azure/iotedge/commit/168a79c2b494dd60e2dbd23adbbb6edf6f70fe01) )
* Add check `up_to_date_config`  ( [8af0fe818](https://github.com/Azure/iotedge/commit/8af0fe81812d9e2b4d8551d02615f3d39b8409d8) )
* Add `iotedge config mp` to create a super-config with a manual-provisioning connection string.  ( [8a9787745](https://github.com/Azure/iotedge/commit/8a9787745f4e7dc38a70ad4c65190e793f087cb9) )
* Bump aziot version  ( [bb6d7aeb0](https://github.com/Azure/iotedge/commit/bb6d7aeb0f068386774fc17045397e80d2280bb1) )
* Add optional proxy argument to iotedge  ( [6b0c6c5d8](https://github.com/Azure/iotedge/commit/6b0c6c5d8797462c7f6ca8c5c2d1cde4925c3279) )
* Fix package purge when aziot-edged is running  ( [73da8adcc](https://github.com/Azure/iotedge/commit/73da8adcc60ed527123b8d201758dc9c697eb402) )
* Ignore validity in cert API requests  ( [a526d6306](https://github.com/Azure/iotedge/commit/a526d630636b7874727e4ab38aa12ce827e10d1d) )
* Update postrm to delete iotedge user on purge  ( [1c0fc8cd7](https://github.com/Azure/iotedge/commit/1c0fc8cd749929acea3774c4726b5385440c776b) )
* Fix license type in aziot-edge.spec  ( [062592e3b](https://github.com/Azure/iotedge/commit/062592e3be87e1f5223bf3d19ac628b2c64baad3) )
* Fix from bugbash  ( [c6a9bbb44](https://github.com/Azure/iotedge/commit/c6a9bbb44737bac51c8945ed86bf776ef9a8279a), [7245c8e05](https://github.com/Azure/iotedge/commit/7245c8e053b97596dd05a6c70160d4569f77bee4) )
* Implement workaround for nested Edge until identityd supports `parent_hostname`.  ( [dc7c92944](https://github.com/Azure/iotedge/commit/dc7c92944beb3747c6f5341321025c9b541056f6) )
* Convert iotedged config to TOML, and implement `iotedge config`  ( [d0978bf63](https://github.com/Azure/iotedge/commit/d0978bf63bdd5624543680424452ee5c08fe285a) )
* Skip latest version check in nested scenarios  ( [941479382](https://github.com/Azure/iotedge/commit/9414793823f127eef12c4c0a2313d512e05df4a1) )


## Other Modules
* Azure Functions Module supports only Amd64  ( [c57446255](https://github.com/Azure/iotedge/commit/c57446255b9d4a04a15271d728b89268d4c35bf1) )
* Upgrade to latest Rust version  ( [9f674bdf5](https://github.com/Azure/iotedge/commit/9f674bdf5e47b21d8ad24ee983f7259fd9379bd8) )
* Bump Device SDK to latest LTS version  ( [90e5b3264](https://github.com/Azure/iotedge/commit/90e5b3264ac0befe1eeebce898f01635f4ac7d14) )
* Restrict TLS protocol to 1.2 for ApiProxy modules  ( [4a76a20b1](https://github.com/Azure/iotedge/commit/4a76a20b142fd59e6bb44110e1ecd6e6519fc1d7) )
* Update ARM32 and ARM64 images to use Alpine  ( [059aaea2d](https://github.com/Azure/iotedge/commit/059aaea2d23d11bfb5b46dac3b28f9a563395647) )
* Build docker images with embedded metadata  ( [a458af376](https://github.com/Azure/iotedge/commit/a458af376177adacb25349cc8f59df8aae9e1a15) )
* Api proxy image update  ( [cca4ae51d](https://github.com/Azure/iotedge/commit/cca4ae51dec51e57a2d8fc0d7c9d55627d70f807) )
* Remove `failure` dependency  ( [496c89924](https://github.com/Azure/iotedge/commit/496c89924e53b5225b0d5cff813b05e3acb10dae) )
* Migrate to Dotnet 6  ( [37234e02b](https://github.com/Azure/iotedge/commit/37234e02b500e6930d389275ac09a5aee80f7445) )
* Update `regex` to 1.5.5  ( [9f0f7f424](https://github.com/Azure/iotedge/commit/9f0f7f42472f658893ff876ad730338ab9833590) )
* Fix API proxy for special characters  ( [26ab9c135](https://github.com/Azure/iotedge/commit/26ab9c1354c2d609f2a7722cd04cd478bb062b1e) )
* Update references to the default branch  ( [04ee9751f](https://github.com/Azure/iotedge/commit/04ee9751f08691b1bff157829a1498fc71998ab5) )
* Upgrade Rust toolchain  ( [ab700e82a](https://github.com/Azure/iotedge/commit/ab700e82ad6ca15140832fbb9970075e2e331073) )
* Update `Microsoft.Azure.Devices.Client` from 1.36.3 to 1.36.4  ( [19beaae55](https://github.com/Azure/iotedge/commit/19beaae556897a2b0676c455523df136ea016e73) )
* Update Base Images for a Security Patch  ( [e6d52d6f6](https://github.com/Azure/iotedge/commit/e6d52d6f6b0eb76e7ef250f3fcdeaf38e467ab4f), [7e0c1a5d3](https://github.com/Azure/iotedge/commit/7e0c1a5d38755a04ee0b802723a9474cd50eec87), [704250b04](https://github.com/Azure/iotedge/commit/704250b041be242938eb8a238513977a5b452600), [b592e4776](https://github.com/Azure/iotedge/commit/b592e47760d0b071c18a13c54de9125d990abcfd), [5cb16fb5d](https://github.com/Azure/iotedge/commit/5cb16fb5d8f2ad8f1b7df4092c29333af837df9b), [addda2b60](https://github.com/Azure/iotedge/commit/addda2b60ec8d2ef69b9f0fe8832a91783eff74b), [b00a78805](https://github.com/Azure/iotedge/commit/b00a78805384deff1ce2c252302a260f31c31faa) )
* Update tokio to 1.15.0  ( [c941f0605](https://github.com/Azure/iotedge/commit/c941f06055e827e75c606cd02dab37aebd4b87ed) )
* Build rocksdb and arm images in amd64 hosts (ubuntu 20.04 hosts)  ( [2ad61fa31](https://github.com/Azure/iotedge/commit/2ad61fa31f0baba86d9446cd3550cdd89abfa1da) )
* Add delay between nginx crashes  ( [2f6bfb30b](https://github.com/Azure/iotedge/commit/2f6bfb30bd1430aa139b85fdf4de08920251bdde) )
* Add `ContentEncoding` and `ContentType` to support routing and Event Grid for TempSensor Module ( [e261b4b43](https://github.com/Azure/iotedge/commit/e261b4b4364ee7e09b7b438f062986a9b0ea61c8) )
* Update SDK from 1.36.2 to 1.36.3 to fix connectivity issues  ( [865b275b4](https://github.com/Azure/iotedge/commit/865b275b4703cd6c41c2f63623994b133d3e3827) )
* Change so nginx doesn't start as root by mistake  ( [6769f901e](https://github.com/Azure/iotedge/commit/6769f901e7d6d75b63cded44381fbfad5fb62be9) )
* Update TempFilterFunc binding protocol to `Amqp_Tcp_Only`   ( [72266d057](https://github.com/Azure/iotedge/commit/72266d057848a68f5f5c9844440d83e656935478) )
* Update SDK references to fix Dotnetty bug  ( [0750a4414](https://github.com/Azure/iotedge/commit/0750a44146c81ea3cce9bc447016b535612502fc) )
* Fix functions sample on centos  ( [ada39f5c6](https://github.com/Azure/iotedge/commit/ada39f5c68e856e42b86afc4a7c4f49a823fecc9) )
* Api proxy image update  ( [5288a2763](https://github.com/Azure/iotedge/commit/5288a2763adfca22aa4e88d408e707ca0df30022) )
* Update edgelet to use tokio 1  ( [4c2f173b3](https://github.com/Azure/iotedge/commit/4c2f173b30025c77e7939b48a968c9a50f269559) )
* Update System.Text.Encodings.Web  ( [ad88f8e32](https://github.com/Azure/iotedge/commit/ad88f8e3275d84b5a92215930559de1c43835181) )
* Fix API proxy cache  ( [a6064515c](https://github.com/Azure/iotedge/commit/a6064515cda69f198d0143296e414e96ebcead00) )
* RUSTSEC fixes  ( [e24cec895](https://github.com/Azure/iotedge/commit/e24cec89539faa06f7215049ee94ca351af0b698) )
* Run API proxy as nginx user  ( [05c9f7852](https://github.com/Azure/iotedge/commit/05c9f7852b4b613b2a80682a6c7558304b224807) )
* Not running api proxy as root  ( [675f0e3d0](https://github.com/Azure/iotedge/commit/675f0e3d0aab6c1bb664bddc498a198eb377c771) )
* Change ssl protocols and ciphers  ( [e369ef883](https://github.com/Azure/iotedge/commit/e369ef8837cc4baa779a61bb8b15c9d31c8a63fc) )
* Update functions packages  ( [f52a88457](https://github.com/Azure/iotedge/commit/f52a88457730b5d91357c3eb9bc5dc7128275cea) )
* Update tokio and hyper dependencies  ( [39bd6dc31](https://github.com/Azure/iotedge/commit/39bd6dc31690ad5d65e9bb4a3062f50555149216) )
* Add ACR unit tests for config parser  ( [ab6304d68](https://github.com/Azure/iotedge/commit/ab6304d6804e1a809fab3bb4d2e6d2d1fe225262) )
* Fix user configuration  ( [73da8f688](https://github.com/Azure/iotedge/commit/73da8f6887346edd8d3217d87000d7c6bb941f01) )
* Fix setting up env var when receiving new config  ( [d0c1bf84a](https://github.com/Azure/iotedge/commit/d0c1bf84a1f325bd5f65f81bc7030fc7152dfa4d) )
* Change default uid  ( [b443b0c2f](https://github.com/Azure/iotedge/commit/b443b0c2f130bbc18060dff9cd2dca26ca6e014e) )
* Fix merge problem.  ( [1947aea51](https://github.com/Azure/iotedge/commit/1947aea51373094c1f1ff6d37eb020bf1f9aa2b1) )
* Fix potential instability in iotedged after UploadSupportBundle fails.  ( [4c6f5d727](https://github.com/Azure/iotedge/commit/4c6f5d7270e8f9ea718c4909bb4c1d1a0ecb2156) )
* edgehub-proxy update RUSTSEC deps  ( [e44dd81a6](https://github.com/Azure/iotedge/commit/e44dd81a6adedc43f50ab95eb743c2f95505448e) )
* Adding boolean expression parsing to API proxy  ( [d1206d949](https://github.com/Azure/iotedge/commit/d1206d94937c0246663dbe4b7fb791d46482e82a) )
* Update rust toolchain to 1.52.1  ( [e5218d1e7](https://github.com/Azure/iotedge/commit/e5218d1e70ce71929cc1cabf2965e7a196ff9614) )
* Simplify config parsing  ( [5ade90d4c](https://github.com/Azure/iotedge/commit/5ade90d4c92b7e0a848897467a2c8b2914457031) )
* Update functions to 3.0  ( [124a20cd4](https://github.com/Azure/iotedge/commit/124a20cd462218d83f6f628824057898a2230bb4) )
* Change config on initial twin  ( [5421f9e7b](https://github.com/Azure/iotedge/commit/5421f9e7b88657b6e2d933380d82da0d815cc0cf) )
* Hide SAS key  ( [9e8323524](https://github.com/Azure/iotedge/commit/9e83235244070453dc2f52f1f74b3964bca9a265) )
* Upgrade api-proxy module to tokio1  ( [8155604c2](https://github.com/Azure/iotedge/commit/8155604c2a46181ec3e6c382e658a8b2d20d85fd) )
* Update rust toolchain to 1.51  ( [0f1d90c7c](https://github.com/Azure/iotedge/commit/0f1d90c7c7cec47a087f749da8245c9f6e84a165) )
* Fix API proxy race condition (#4768)  ( [d2c331d60](https://github.com/Azure/iotedge/commit/d2c331d605a846911019364a31a7d098e1e2fc45) )
* Fix Api proxy indirection  ( [d129a0719](https://github.com/Azure/iotedge/commit/d129a07190ac8cb3ea92545ff900a98584374aa7) )
* Merge api proxy edge hub pr  ( [8ac0a7462](https://github.com/Azure/iotedge/commit/8ac0a74626c7f0e21df67f9bb5fe4fbb17d0da36) )
* `iotedge check` improvements for nested edge  ( [22819dd7f](https://github.com/Azure/iotedge/commit/22819dd7fba0567a7273e3c3219630ccfef5ce50) )
* Change nginx from alpine to ubuntu bionic  ( [89ad3dab0](https://github.com/Azure/iotedge/commit/89ad3dab002963220752d124b8a3f9fb0219f06a) )
* Fix arm64 image  ( [17d7cadab](https://github.com/Azure/iotedge/commit/17d7cadab8ff3ad89cc672b60efbab95c3a3f705) )
* Remove references to iiot branches  ( [436bada3a](https://github.com/Azure/iotedge/commit/436bada3ab70fe51ff0f0fe6db35cf28c5918f97) )
* Fix api proxy  ( [1d7e0a1bb](https://github.com/Azure/iotedge/commit/1d7e0a1bb68b783d075cada3a326c0daf420c09a) )
* Revert to nginx image  ( [c2bce19df](https://github.com/Azure/iotedge/commit/c2bce19df21160d9783c85677031b5944742829e) )


# 1.2.10 (2022-05-27)
## Edge Agent
### Bug Fixes
* Restore SystemInfo structure for product information ( [bf31d16](https://github.com/Azure/iotedge/commit/bf31d16705d3fe63a9fc411e84635348a78daeb6) )
* Update Base Image to address security vulnerabilities [CVE-2022-23267](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-23267) [CVE-2022-29117](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-29117) [CVE-2022-1271](https://ubuntu.com/security/CVE-2022-1271)

## Edge Hub
### Bug Fixes
* Configurable task for cancelling hanging upstream calls( [12b52ba](https://github.com/Azure/iotedge/commit/12b52babe6fba24b136971a5d1ce1b8df387b1d7) )
* Update Base Image to address security vulnerabilities [CVE-2022-23267](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-23267) [CVE-2022-29117](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-29117) [CVE-2022-1271](https://ubuntu.com/security/CVE-2022-1271)

## aziot-edge
### Bug Fixes
* Improve error logging for WorkloadManager ( [f2e5a47](https://github.com/Azure/iotedge/commit/f2e5a47bb3fd109b5523cb2ae9849401f0fcc99d) )
* Fix exit code when restarting due to reprovision( [d7d98d0](https://github.com/Azure/iotedge/commit/d7d98d0b606018c6cf5a60f15c44c01c663e0655) )
* Mariner 2.0 Package Build for IoTEdge( [63273b1](https://github.com/Azure/iotedge/commit/63273b1a6b543eee64d1cb6a8a8fdd8caed47c63) )


### Features
*  Flatten additional properties of metrics ( [4983128](https://github.com/Azure/iotedge/commit/12b52babe6fba24b136971a5d1ce1b8df387b1d7) )


# 1.2.9 (2022-04-04)
## Edge Agent
### Bug Fixes
* Dev identity issues when switching identities ( [fb8d034](https://github.com/Azure/iotedge/commit/fb8d034626e43c413371274d7f9f685617c5b03f) )
* Update regex to 1.5.5 ( [cb20b6b](https://github.com/Azure/iotedge/commit/cb20b6b2aca25ca09e58713e35e116879e323be3) )
* Device product information ( [477814d](https://github.com/Azure/iotedge/commit/477814d9caaeba0c5318e418d1b44ddfc5206249) )


## Edge Hub
### Bug Fixes
* AMQP CBS token message dispose ( [8670979](https://github.com/Azure/iotedge/commit/86709794fd42149368d632ef534f82c56db3ad79) )
* Dev identity issues when switching identities ( [fb8d034](https://github.com/Azure/iotedge/commit/fb8d034626e43c413371274d7f9f685617c5b03f) )


## aziot-edge
### Bug Fixes
* Update tokio, rayon, and crossbeam to latest compatible versions( [d468058](https://github.com/Azure/iotedge/commit/d468058d294453feebc92870f0f7cae7a6ca4a3d), [a0f148e](https://github.com/Azure/iotedge/commit/a0f148e88c82ff42ba1852b8a8f23fee0882075c) )
* Update regex to 1.5.5 ( [cb20b6b](https://github.com/Azure/iotedge/commit/cb20b6b2aca25ca09e58713e35e116879e323be3) )
* Device product information ( [477814d](https://github.com/Azure/iotedge/commit/477814d9caaeba0c5318e418d1b44ddfc5206249) )


# 1.2.8 (2022-02-24)
## Edge Agent
### Bug Fixes
* Fix underflow possibility on ColumnFamilyDbStore  ( [bc78f1c](https://github.com/Azure/iotedge/commit/bc78f1c0b0ab0dfa305b817c030b82cea035f6e0) )
* Remove BouncyCastle dependency ( [403ca87](https://github.com/Azure/iotedge/commit/403ca87bc228421c8913c1431687267dee9112a7), [7589457](https://github.com/Azure/iotedge/commit/7589457deec0f2735711cb8973cac0f0e17046c1) )
* Update `Microsoft.Azure.Devices.Client` SDK ( [4b7570f](https://github.com/Azure/iotedge/commit/4b7570f12b2de0875e29b19964c935e693933e82) )


## Edge Hub
### Bug Fixes
* Fix underflow possibility on ColumnFamilyDbStore  ( [bc78f1c](https://github.com/Azure/iotedge/commit/bc78f1c0b0ab0dfa305b817c030b82cea035f6e0) )
* Remove BouncyCastle dependency ( [403ca87](https://github.com/Azure/iotedge/commit/403ca87bc228421c8913c1431687267dee9112a7), [7589457](https://github.com/Azure/iotedge/commit/7589457deec0f2735711cb8973cac0f0e17046c1) )
* Restart EdgeHub upon certificate renewal ( [c5e90a7](https://github.com/Azure/iotedge/commit/c5e90a75e9a89ff612ad8c60241b0039464c298d) )
* Update `Microsoft.Azure.Devices.Client` SDK ( [4b7570f](https://github.com/Azure/iotedge/commit/4b7570f12b2de0875e29b19964c935e693933e82) )
* Workaround for windows-certificate import problem for EdgeHub in Visual Studio debug runs ( [0ed0c71](https://github.com/Azure/iotedge/commit/0ed0c710e2fc38c6feda7c39cb71b6625e20e87d) )


## aziot-edge
### Bug Fixes
* Remove `sudo` from `iotedge check` for local proxy setting check ( [5976efb](https://github.com/Azure/iotedge/commit/5976efb411b3edfd860494e081ed835637c96529) )
* Update vulnerable regex package ( [a34fd5b](https://github.com/Azure/iotedge/commit/a34fd5bca7e58c01819da56dd73df817fbefeb7e), [fe7de0b](https://github.com/Azure/iotedge/commit/fe7de0b45363594d1f23b575eddb2947d38ace6a) )


# 1.2.7 (2022-01-19)
## Edge Agent
### Bug Fixes
* Update base image for security patch ( [8194a93](https://github.com/Azure/iotedge/commit/8194a93ab147658ca545dd8da97c0088904f284d) )


## Edge Hub
### Bug Fixes
* Update base image for security patch ( [8194a93](https://github.com/Azure/iotedge/commit/8194a93ab147658ca545dd8da97c0088904f284d) )
* Update vulnerable nix version ( [ca6958f](https://github.com/Azure/iotedge/commit/ca6958f7a3995c43973e4fbd006c1be737b60fe8) )


## aziot-edge
### Bug Fixes
* Removed Moby check ( [27a14d8](https://github.com/Azure/iotedge/commit/27a14d817d8de78b562691945689fa4400de56b6) )
* Fix for workload socket issue for concurrent module creation ( [5712dcc](https://github.com/Azure/iotedge/commit/5712dcc28498121d890082d5c884d9855cc40efd) )
* Addition of device ID to edge CA common name to support large number of devices ( [6627c7a](https://github.com/Azure/iotedge/commit/6627c7a835ed6b252da00faa29b9d9b8e5cd501b) )


### Features
* New IoTedge check called proxy-settings which verifies proxy settings ( [4983128](https://github.com/Azure/iotedge/commit/49831285a02de9189ac338237aaa0f529a72c559) )


# 1.2.6 (2021-11-12)
## Edge Agent
### Bug Fixes
* Revert [2677657](https://github.com/Azure/iotedge/commit/26776577a4eec9414108e29d2bb4263c9b2d8b76), which inadvertently disabled duration and Unix timestamp formats in the since and until arguments of GetModuleLogs and UploadModuleLogs direct methods ( [f7f4b89](https://github.com/Azure/iotedge/commit/f7f4b89e697808365f81b7ada622bdb4bf87e722) )

# 1.2.5 (2021-11-09)
## Edge Agent
### Bug Fixes
* Add `RocksDB_MaxManifestFileSize` env var to Edge Agent and Edge Hub ( [c9c4b29](https://github.com/Azure/iotedge/commit/c9c4b2979c5986597c86bb8630ddd4a97df490f1) )
* Recreate edgeAgent when not Running, Stopped, or Failed ( [c5d6176](https://github.com/Azure/iotedge/commit/c5d6176af44e3650507d394411c824b1019204f8) )
* Update SDK to 1.36.3 ( [f12d7ca](https://github.com/Azure/iotedge/commit/f12d7ca0ff6ea826d09989b5b5488af44c366a6f) )
* Update Base Images for a security patch ( [d6e3657](https://github.com/Azure/iotedge/commit/d6e3657c5abac3752138b3c29955556aad8a8b30) )
* Restricting EdgeAgent identity parallel operation calls to edged to 5 ( [2391cd9](https://github.com/Azure/iotedge/commit/2391cd992af91633293e5f27fde07cd1522888f3) )


## Edge Hub
### Bug Fixes
* Remove WebSocket Ping KeepAlives ( [2d451cc](https://github.com/Azure/iotedge/commit/2d451cc576d5f934c3767ebb894089e7d722b217) )
* Update SDK to 1.36.3 ( [f12d7ca](https://github.com/Azure/iotedge/commit/f12d7ca0ff6ea826d09989b5b5488af44c366a6f), [9a2a526](https://github.com/Azure/iotedge/commit/9a2a52659b75d4964254607d253b3d7189ae07b3) )
* Update Base Images for a security patch ( [d6e3657](https://github.com/Azure/iotedge/commit/d6e3657c5abac3752138b3c29955556aad8a8b30) )
* Detect fail-over from Iot Hub and SDK behavior and disconnect from IoT Hub ( [52c563a](https://github.com/Azure/iotedge/commit/52c563a506ff56d80905dcb054dfa918104a1fa2) )
* Fix `edgehub_queue_len` metric ( [487890d](https://github.com/Azure/iotedge/commit/487890db566cb31cef7e8b83dff34526f2c74608) )


## Azure Functions Module Sample
### Bug Fixes
* Update TempFilterFunc binding protocol to Amqp_Tcp_Only ( [a5e559c](https://github.com/Azure/iotedge/commit/a5e559c823528fc38c83cf110ccbacca8549bda4) )
* Update Base Images for a security patch ( [d6e3657](https://github.com/Azure/iotedge/commit/d6e3657c5abac3752138b3c29955556aad8a8b30) )
* Update SDK to 1.36.3 ( [f12d7ca](https://github.com/Azure/iotedge/commit/f12d7ca0ff6ea826d09989b5b5488af44c366a6f) )


## aziot-edge
### Bug Fixes
* Disable connection pooling for docker client ( [12e12cf](https://github.com/Azure/iotedge/commit/12e12cff639560b554ab3c84ff8d2a1acaa5e6fe) )
* Allows an issued Edge CA certificate to be specified in the super config ( [6368eb6](https://github.com/Azure/iotedge/commit/6368eb60e09e0f24e78dd0a936b31223437968e9) )
* Fix workload socket permission denied ( [861aceb](https://github.com/Azure/iotedge/commit/861acebc55d8b4737451edfaa2d12ed2e8dad6ed) )
* Backport EST documentation and update configuration template ( [3822152](https://github.com/Azure/iotedge/commit/382215262b3320c15f2694617ac0e77f8f86a251) )
* Fix typo in template configuration ( [d0978ba](https://github.com/Azure/iotedge/commit/d0978bacaeeecbd0fe4611b6c38bbcbc10044d9a) )


# 1.2.4 (2021-09-29)
## Edge Agent
### Bug Fixes
* Delay frequent twin pulls on reconnect ( [95b4441](https://github.com/Azure/iotedge/commit/95b4441402fbab9d292f0d962ff89c03e28b848a) )
* Make sure to dispose HttpContentStream when done reading module logs ( [47011b1](https://github.com/Azure/iotedge/commit/47011b1ed0270182afdadb3eba27220fd4dc38bf) )
* Update Base Images for a Security Patch ( [3b83e7f](https://github.com/Azure/iotedge/commit/3b83e7f76eaa083f73a9ee6b3f9a8973f8268f5e), [56e96cd](https://github.com/Azure/iotedge/commit/56e96cdf9970c057b7a8028c508f6b0b997f0b62) )
* `$upstream` support for container registry address ( [ebdb5be](https://github.com/Azure/iotedge/commit/ebdb5becf9a1c5f1fb2aa27794b6e69b0b754d52) )
* Fix edgeAgent creates rogue ModuleClients when encounters an error ( [4b87cc9](https://github.com/Azure/iotedge/commit/4b87cc97fc202fbf72d08075dd99d891d1637f9f) )
* Update SDK to fix dotnetty bugs ( [ea818f0](https://github.com/Azure/iotedge/commit/ea818f03f288e818626180ccc8319aea184835f9) )


## Edge Hub
### Bug Fixes
* Add a component name to message properties ( [4f36aba](https://github.com/Azure/iotedge/commit/4f36aba0522b5efe547fefc27a2d695f593885e7) )
* Update Base Images for a Security Patch ( [3b83e7f](https://github.com/Azure/iotedge/commit/3b83e7f76eaa083f73a9ee6b3f9a8973f8268f5e), [56e96cd](https://github.com/Azure/iotedge/commit/56e96cdf9970c057b7a8028c508f6b0b997f0b62) )
* Enable leaf identity creation ( [358aeb7](https://github.com/Azure/iotedge/commit/358aeb76f4c7b59929def8bcb977b51c2191066f) )
* Update SDK to fix dotnetty bugs ( [ea818f0](https://github.com/Azure/iotedge/commit/ea818f03f288e818626180ccc8319aea184835f9) )
* Use separate flag for MQTT Buffer pooling ( [38f34f6](https://github.com/Azure/iotedge/commit/38f34f62180b8e97c1ffd3d15cede96ddcd13d38) )


## Azure Functions Module Sample
### Bug Fixes
* Update Azure Functions packages ( [d8ea036](https://github.com/Azure/iotedge/commit/d8ea036f4522d77e2e9498cde80e1b69d50b44c8) )
* Update Base Images for a Security Patch ( [3b83e7f](https://github.com/Azure/iotedge/commit/3b83e7f76eaa083f73a9ee6b3f9a8973f8268f5e), [56e96cd](https://github.com/Azure/iotedge/commit/56e96cdf9970c057b7a8028c508f6b0b997f0b62) )


## MQTT Broker
### Bug Fixes
* Fix find_first_block seek logic ( [1c9b39a](https://github.com/Azure/iotedge/commit/1c9b39a4bf00d88d619164f8332df414a56ba1da) )


## aziot-edge
### Bug Fixes
* Fix host cpu metric incorrectly reported at 100% ( [876900a](https://github.com/Azure/iotedge/commit/876900a3644eba5a14e09ba3d580e25eeff88f4b) )
* Add timeout to support bundle calls ( [16ede21](https://github.com/Azure/iotedge/commit/16ede21d0d454177fad8b1ce906f012f56c02d3e) )
* Introduce `allow_elevated_docker_permissions` flag ( [175603c](https://github.com/Azure/iotedge/commit/175603c634881e081979d507f94baf3c9f7f642f) )
* RUSTSEC Security Update ( [24e4d27](https://github.com/Azure/iotedge/commit/24e4d272415de71e0ed75b41089e1471c35cc8c9), [b59a089](https://github.com/Azure/iotedge/commit/b59a089e6fa3fbf1d869e37e5e2a5113449b37e3), [5e2ba80](https://github.com/Azure/iotedge/commit/5e2ba80786402615bebff078b67eab63313ffc39), [790a8f9](https://github.com/Azure/iotedge/commit/790a8f99e7f24c86c4c491a6e0eb6d2c388b47ec), [c6d805b](https://github.com/Azure/iotedge/commit/c6d805b1753ba1a69bf05b2e4f0eda312545d347) )
* $upstream support for container registry address ( [ebdb5be](https://github.com/Azure/iotedge/commit/ebdb5becf9a1c5f1fb2aa27794b6e69b0b754d52) )
* Improve Workload Manager logging and cleanup ( [febd7a2](https://github.com/Azure/iotedge/commit/febd7a2d1467b891c0dea0edff00c90c6b320e0b) )
* Update cargo dependencies ( [f147f12](https://github.com/Azure/iotedge/commit/f147f12881c91c029de49c1aba8f509d6c3eed47) )
* Update Azure IoT Identity Service components to version 1.2.3 ( [fea0ae2](https://github.com/Azure/iotedge/commit/fea0ae2b8f4ef41a9007294b2cb072fef3be5a61) )


# 1.2.3 (2021-06-30)
## aziot-edge
### Bug Fixes
* Fix `iotedge check` recommending an old version of aziot-identity-service. ( [87381d9](https://github.com/Azure/iotedge/commit/87381d992ea7edd6f12376b14299269e6ab0bbe3) )


# 1.2.2 (2021-06-23)
## Edge Agent
### Bug Fixes
* Properly dispose UDS for Workload Client. ( [472cee5](https://github.com/Azure/iotedge/commit/472cee580101cfd5999492ef3760d5038679680e), [f9cdb59](https://github.com/Azure/iotedge/commit/f9cdb5902a8e13b80bbd8040323fb0729f085dc3) )
* Update Base Images for Security Vulnerability ( [d0e6113](https://github.com/Azure/iotedge/commit/d0e6113e4454ef5f23909c396f793833f14649e1) )

### Features
* Use Docker Timestamp When Log Timestamp is not Available in JSON-formatted log. ( [d336d08](https://github.com/Azure/iotedge/commit/d336d085f5503747fb5f194e3cd261c0d2b91aea) )


## Edge Hub
### Bug Fixes
* Update Base Images for Security Vulnerability ( [d0e6113](https://github.com/Azure/iotedge/commit/d0e6113e4454ef5f23909c396f793833f14649e1) )
* Propagate back error code from edgeHub ( [421347d](https://github.com/Azure/iotedge/commit/421347dff842eff6e552462b967f6c84e1982b29) )


## Diagnostic Module
### Bug Fixes
* Fix potential instability in iotedged after UploadSupportBundle fails. ( [f567e38](https://github.com/Azure/iotedge/commit/f567e3870633209fac609c37d71d715e39d73e1a) )
* Update Base Images for Security Vulnerability ( [d0e6113](https://github.com/Azure/iotedge/commit/d0e6113e4454ef5f23909c396f793833f14649e1) )


## Temperature Filter Function Module
### Bug Fixes
* Update Temperature Filter Function sample module to be using .NET3.0. ( [adf8878](https://github.com/Azure/iotedge/commit/adf88788fb25873a3db90b1cb775fdd97afbd8c0) )


## aziot-edge
### Bug Fixes
* Fix provisioning behavior when DPS changes. ( [c6e8900](https://github.com/Azure/iotedge/commit/c6e890040945544112d56d377cd22655d5b0dd05) )
* Limit sysinfo crate FDs usage. ( [5947981](https://github.com/Azure/iotedge/commit/5947981c06b3677fbf403e00f090d180f5737050) )

### Features
* Enable aziot-edged in CentOS package. ( [0539cdb](https://github.com/Azure/iotedge/commit/0539cdb31f4e6f53c20245cf3290b4095dd50435) )
* Update IoT Identity Service to version 1.2.1 ( [572de56](https://github.com/Azure/iotedge/commit/572de564ed48f43492d589d63c8bf3d43cd2160c) )


# 1.2.1 (2021-06-01)
## Edge Agent
### Bug Fixes
* Update Base Images for Security Patch. ( [513f721](https://github.com/Azure/iotedge/commit/513f721c38381a32ed968bdb1c489ee0d9cfc243) )


## Edge Hub
### Bug Fixes
* Update bridge config validation. ( [afdc9c2](https://github.com/Azure/iotedge/commit/afdc9c2e8d8fc46c585d7a34376a4e099917e64b) )
* Device scope cache retry for first initialization. ( [3b903a1](https://github.com/Azure/iotedge/commit/3b903a19dcd2b41105664442335f37b357fddbcb) )
* Add validation for null props inside objects inside arrays. ( [c25fcb9](https://github.com/Azure/iotedge/commit/c25fcb94729c795d78de802f9e76c6e096050335) )
* Adds SharedAccessSignature to repo with fix for vulnerability. ( [60d411c](https://github.com/Azure/iotedge/commit/60d411c5d6737564f75469b4ab35a1aee8306dee) )
* Update GetModuleLogs method when tail + since + until options are provided. ( [2b650a8](https://github.com/Azure/iotedge/commit/2b650a8b90d5b51299ef24c002e5af39c481c253) )
* Fix edgehub queue len metric ( [4068369](https://github.com/Azure/iotedge/commit/4068369c4e32326809c3d5cfde33a8da0215dcfc) )
* Update Base Images for Security Patch. ( [513f721](https://github.com/Azure/iotedge/commit/513f721c38381a32ed968bdb1c489ee0d9cfc243) )


### Features
* Restore device scopes from older store. ( [c90245b](https://github.com/Azure/iotedge/commit/c90245bec35e7dae06d50e171556961de8e718de) )


## aziot-edge
### Features
* Introduce Timestamps Option via mgmt.sock. ( [37c661b](https://github.com/Azure/iotedge/commit/37c661bcbae1b2b2506e54d102a14ea1b6f8bb3c) )


# 1.2.0 (2021-04-9)
## AWARENESS
This release contains a significant refactoring to the IoT Edge security daemon. It separates out the daemon's functionality for provisioning and providing cryptographic services for Linux-based devices into a set of stand-alone system services. Details on these individual system services can be found in the [Overview](https://azure.github.io/iot-identity-service) of the related github repository in which they reside.

### Impact to Edge modules
Every attempt has been made to ensure that the APIs on which Edge modules depend will remain unaffected and backward compatible. Issues affecting Edge modules will be treated with the highest priority.

### Impact to installing / configuring IoT Edge
The refactoring does affect the packaging and installation of IoT Edge. While we've attempted to minimize the impact of these there are expected differences. For more details on these changes please refer to the discussion of [Packaging](https://github.com/Azure/iotedge/blob/main/doc/packaging.md).


## Edge Agent
### Bug Fixes
* Update Base Images for Security Vulnerability ( [ac0da07](https://github.com/Azure/iotedge/commit/ac0da07aab45bb36dc008a1ea373e979b50c0e15) )
* Update SDK version ( [46c2d20](https://github.com/Azure/iotedge/commit/46c2d20078470c5fa2f1fb1d9d6dc7516fb5ec6b) )
* Update .NET Core Runtime base images ( [8f9e22e](https://github.com/Azure/iotedge/commit/8f9e22e39818cf0573fa5c401e06c94ea77cf981) )


## Edge Hub
### Bug Fixes
* Update http client timeout for scope sync ( [69d8c0c](https://github.com/Azure/iotedge/commit/69d8c0cba260a4660b9132d2fe476e1390110756) )
* Add caching to TokenProvider ( [8988456](https://github.com/Azure/iotedge/commit/8988456377154de075e83d6a896778d7200a1a61) )
* Update Base Images for Security Vulnerability ( [ac0da07](https://github.com/Azure/iotedge/commit/ac0da07aab45bb36dc008a1ea373e979b50c0e15) )
* Fix edgeHub children mismatched leaf device subscriptions ( [39c600f](https://github.com/Azure/iotedge/commit/39c600f40c0e2c8a8ae03bb7777231f55beba03d) )
* Improve registry controller error message ( [0b0a40e](https://github.com/Azure/iotedge/commit/0b0a40e93ad2eca395b17706157fd6e06a510cce) )
* Add edgeHub identity to the scopes cache at the startup ( [621a2ad](https://github.com/Azure/iotedge/commit/621a2ad873feba1114177c13a7d934edceed5b71) )
* Improve AMQP messages `Batchable` delay ( [e88c2b9](https://github.com/Azure/iotedge/commit/e88c2b9a661df29f184fb4c2956b61d7c6d9b169) )
* Fix websocket authentication with certificates over ApiProxy ( [6c48961](https://github.com/Azure/iotedge/commit/6c48961f6c12c46cdd6c1e1e5f83d7139e9eeaf8) )
* Fix EdgeHub dropping routing RP upon info forwarding ( [fa60e52](https://github.com/Azure/iotedge/commit/fa60e528706371c2bff0c7a7c3f795b737678646) )
* Fix registry API On-behalf-of calls authentication ( [64fb35b](https://github.com/Azure/iotedge/commit/64fb35b7a3ebe1537784ac0912a106e5115b0a9b) )
* Fix getDeviceAndModuleOnBehalfOf to check if target device is in scope ( [5e1028e](https://github.com/Azure/iotedge/commit/5e1028ec8db3b79c61969abeffe1b2a3701adf8a) )
* Fix resolving BrokeredCloudProxyDispatcher ( [5fc8dfb](https://github.com/Azure/iotedge/commit/5fc8dfb9220a0275373873def88c56a0ddc035c8) )
* Update SDK version ( [46c2d20](https://github.com/Azure/iotedge/commit/46c2d20078470c5fa2f1fb1d9d6dc7516fb5ec6b) )
* Fix twins reconnection issue for clients with MQTT upstream ( [eb6051c](https://github.com/Azure/iotedge/commit/eb6051c8959d4208afd0d866d76210c7a59f3489) )
* Support new SDK subscription optimization ( [1e3ee4b](https://github.com/Azure/iotedge/commit/1e3ee4bc92716bf545b7b97760a6d53bcda346e6) )
* Propagate close() upon cloud proxy for CloudConnection ( [b5177de](https://github.com/Azure/iotedge/commit/b5177ded0c680b370f60587adab9f7f746af71a0) )
* Update .NET Core Runtime base images ( [8f9e22e](https://github.com/Azure/iotedge/commit/8f9e22e39818cf0573fa5c401e06c94ea77cf981) )
* Drop messages when device is not in scope and auth mode is the scope ( [7c08b9c](https://github.com/Azure/iotedge/commit/7c08b9c9ba36b3e4767cab446aaf66e799a897d1) )

### Features
* Move NestedEdgeEnabled out of experimental features ( [ee703c4](https://github.com/Azure/iotedge/commit/ee703c47782d058e1f69a4081673f37b9b563215) )
* Update `iotedge check` for version 1.2.0 ( [db18594](https://github.com/Azure/iotedge/commit/db18594197c1fd0eeacb474f31c95828f689b882), [ee73e76](https://github.com/Azure/iotedge/commit/ee73e76d18874ddfce416098df9096c0d484c63b) )


## aziot-edge
### Bug Fixes
* Fix for expired CA certificate not renewing ( [ac142d1](https://github.com/Azure/iotedge/commit/ac142d137a84f37f7417ade89a2ae2051689b76f) )
* Cache device provisioning state ( [9301f13](https://github.com/Azure/iotedge/commit/9301f13455da30c7ee28bff01f94e3579681ab30) )
* Fix check-agent-image-version check for nested Edge scenarios ( [36d859e](https://github.com/Azure/iotedge/commit/36d859e0f3f05d73493e104afc800c8289c4343d) )
* Import master encryption key in `iotedge config import` ( [01ef049](https://github.com/Azure/iotedge/commit/01ef049d5a26271f122dd28165b62aa7c9877277) )
* Fix `iotedge config apply` not picking up parent hostname because of serde bug ( [b4c600a](https://github.com/Azure/iotedge/commit/b4c600a944b643e3d8c2b10838a0f91df4c89b5a) )
* Read `parent_hostname` configuration from aziot ( [b14db9d](https://github.com/Azure/iotedge/commit/b14db9d4d0c78c635acf1fd66121e5970caf0232) )
* Update serde-yaml version ( [474ce0e](https://github.com/Azure/iotedge/commit/474ce0e24373c55b45dfc53828c562b2a9e6ce40) )
* Enable dynamic provisioning support ( [d9aa3ac](https://github.com/Azure/iotedge/commit/d9aa3ac164c4b94a498b9238bc3f8b2057902147) )
* Fix package purge when aziot-edged is running ( [808a2d7](https://github.com/Azure/iotedge/commit/808a2d796c54033cbac0b9577a86acb33e238454) )
* Ignore validity in cert API requests ( [109ee6a](https://github.com/Azure/iotedge/commit/109ee6adb1db703d039dea25b79bcb6b1fd158dc) )

### Features
* Allow aziot-edge to collect system logs when calling remote support-bundle ( [a0f3725](https://github.com/Azure/iotedge/commit/a0f372505bb2a6482e3462dcae4ddb689fc26b81) )
* `aziotctl system` improvements ( [d62b22f](https://github.com/Azure/iotedge/commit/d62b22f308e016c8ae057931c06353fdc2b0f5bc) )
* Update `iotedge check` & `iotedge config` for version 1.2.0 ( [ee73e76](https://github.com/Azure/iotedge/commit/ee73e76d18874ddfce416098df9096c0d484c63b), [33661f5](https://github.com/Azure/iotedge/commit/33661f5f9ff2c7d8a00cd65cb5429292b0f47461) )
* Document the super-config's agent.config.createOptions value format more clearly ( [76c4b70](https://github.com/Azure/iotedge/commit/76c4b70d2cfadef5f61a723ff4e34d833e58cb48) )
* Introduce `iotedge system stop` ( [ca77919](https://github.com/Azure/iotedge/commit/ca77919172d0b55650d57ecc9a8a88ce3991dbb4) )
* Introduce `iotedge system reprovision` ( [cf62d66](https://github.com/Azure/iotedge/commit/cf62d66ad4a39b716fa858d79f8a95728b2c9a6b) )
* Introduce edgeAgent image version check ( [be8bb55](https://github.com/Azure/iotedge/commit/be8bb556893a13093be80f5c93b8cdf0eca18c82) )
* Allow Connection with trust bundle in the Nested topology ( [fb3f1a3](https://github.com/Azure/iotedge/commit/fb3f1a3df0dc48c4209bbbb0dd6fdd6392ecc249) )
* Introduce check up_to_date_config ( [8e4f685](https://github.com/Azure/iotedge/commit/8e4f68522c07f51f70894f1813fa3b94027c92f9) )
* Introduce optional proxy argument to iotedge ( [a0a883d](https://github.com/Azure/iotedge/commit/a0a883da44499a9f09a4de8794fbac83f752c214) )


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
