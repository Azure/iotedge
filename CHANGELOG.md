# 1.2.7 (2021-01-19)
## Edge Agent
### Bug Fixes
* Update base image for security patch ( [8194a93] (https://github.com/Azure/iotedge/commit/8194a93ab147658ca545dd8da97c0088904f284d)) 

## Edge Agent
### Bug Fixes
* Update base image for security patch ( [8194a93] (https://github.com/Azure/iotedge/commit/8194a93ab147658ca545dd8da97c0088904f284d))

## aziot-edge
### Bug Fixes
* Update vulnerable nix version ( [ca6958f] (https://github.com/Azure/iotedge/commit/ca6958f7a3995c43973e4fbd006c1be737b60fe8))
* New IoTedge check called proxy-settings which verifies proxy settings ( [4983128] (https://github.com/Azure/iotedge/commit/49831285a02de9189ac338237aaa0f529a72c559))
* Removed Moby check ( [27a14d8] (https://github.com/Azure/iotedge/commit/27a14d817d8de78b562691945689fa4400de56b6))
* Fix for workload socket issue for concurrent module creation ( [5712dcc] (https://github.com/Azure/iotedge/commit/5712dcc28498121d890082d5c884d9855cc40efd))
* Addition of device ID to edge CA common name to support large number of devices ( [6627c7a] (https://github.com/Azure/iotedge/commit/6627c7a835ed6b252da00faa29b9d9b8e5cd501b))


# 1.2.6 (2021-11-12)
## Edge Agent
### Bug Fixes
* Revert [2677657](https://github.com/Azure/iotedge/commit/26776577a4eec9414108e29d2bb4263c9b2d8b76), which inadvertently disabled duration and Unix timestamp formats in the since and until arguments of GetModuleLogs and UploadModuleLogs direct methods ([f7f4b89](https://github.com/Azure/iotedge/commit/f7f4b89e697808365f81b7ada622bdb4bf87e722))

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
The refactoring does affect the packaging and installation of IoT Edge. While we've attempted to minimize the impact of these there are expected differences. For more details on these changes please refer to the discussion of [Packaging](https://github.com/Azure/iotedge/blob/master/doc/packaging.md).


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


## MQTT Broker
### Bug Fixes
* Makes egress pump exit with corresponding error ( [08678d5](https://github.com/Azure/iotedge/commit/08678d55e0ea52f2054c64cf4a97c880a4729568) )
* Filter out publication duplicates in MessageLoader ( [0c0536a](https://github.com/Azure/iotedge/commit/0c0536a72cb5d53acf7d4be27bfc2fd38de21730) )
* Fix topic mapping ( [a799291](https://github.com/Azure/iotedge/commit/a799291d06ab412cd1c85045faa3b2b5ba97e684) )
* Fix RingBuffer initialization issue ( [b96b513](https://github.com/Azure/iotedge/commit/b96b513f6af7798bf2958982eb7be905d335760d), [c69ac53](https://github.com/Azure/iotedge/commit/c69ac53148c6fdf049d5d7f6346a470d8c5c407c) )
* Fix flaky proptest ( [484f395](https://github.com/Azure/iotedge/commit/484f395d76514f95f1f08d9efefcdc5a5769d08e) )

### Features
* Merge Broker and Bridge settings ( [07155ad](https://github.com/Azure/iotedge/commit/07155ad3f288ff243b6fbef58b3ae75e7284d9bb) )
* Support RPC subscription requests ( [b86dca7](https://github.com/Azure/iotedge/commit/b86dca7ed898f5eca203ffe281a63238c4cc4924) )
* Improve RingBuffer ( [16c09f6](https://github.com/Azure/iotedge/commit/16c09f6fb8c1aaf359ded13c61ec3fc291ec8d57) )


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


# 1.2.0-rc4 (2021-03-1)
## AWARENESS
This release contains a significant refactoring to the IoT Edge security daemon. It separates out the daemon's functionality for provisioning and providing cryptographic services for Linux-based devices into a set of stand-alone system services. Details on these individual system services can be found in the [Overview](https://azure.github.io/iot-identity-service) of the related github repository in which they reside.

### Impact to Edge modules
Every attempt has been made to ensure that the APIs on which Edge modules depend will remain unaffected and backward compatible. Issues affecting Edge modules will be treated with the highest priority.

### Impact to installing / configuring IoT Edge
The refactoring does affect the packaging and installation of IoT Edge. While we've attempted to minimize the impact of these there are expected differences. For more details on these changes please refer to the discussion of [Packaging](https://github.com/Azure/iotedge/blob/master/doc/packaging.md).


## Edge Agent
### Bug Fixes
* Improve Edge Agent's Prometheus parser ( [a4ae2c1](https://github.com/Azure/iotedge/commit/a4ae2c116296ad086a9f2cb7ccd2f077c5692301) )
* Update Base Images for Security Vulnerability ( [6fe4de3](https://github.com/Azure/iotedge/commit/6fe4de3f16a11d7a294a278b55a07b15fdf7ecd6) )
* Consolidate environment variables for EdgeHub and Broker ( [6167323](https://github.com/Azure/iotedge/commit/6167323e772a61d55f2120c9673360194dd4f1b4) )
* Fix getmodules error filter rfc3339 datetime ( [3a98e83](https://github.com/Azure/iotedge/commit/3a98e830d762317325b77ba8b4958bc0d878c06c) )
* Update current config when a plan is empty ( [087de0b](https://github.com/Azure/iotedge/commit/087de0b59d3354bf17055ff91371bd024494db48) )

## Edge Hub
### Bug Fixes
* Fix MQTT client persistent session's subscription when the client reconnects ( [7e2d191](https://github.com/Azure/iotedge/commit/7e2d191cb8b4cd45bcec7653c523b8f8ab5e707f) )
* Consolidate log levels between EdgeHub and Broker ( [45bcc83](https://github.com/Azure/iotedge/commit/45bcc835b00b940923a99839de4548cdbbabc619) )
* Restrict local authentication for ApiProxy connections to port 8080 ( [11bfea5](https://github.com/Azure/iotedge/commit/11bfea5fb35bf1f1a98ec6ff402343d32bd8dde4) )
* Update Base Images for Security Vulnerability ( [6fe4de3](https://github.com/Azure/iotedge/commit/6fe4de3f16a11d7a294a278b55a07b15fdf7ecd6) )
* Stop EdgeHub if initial twin has no base configuration ( [7701250](https://github.com/Azure/iotedge/commit/770125073da574cff7249bb46328443aa899e58a) )
* Fix Message Store Exception when cleaning up an updated message ( [a478ce8](https://github.com/Azure/iotedge/commit/a478ce84630fc59398aa53969e684d0f01905673) )
* Disable the use of http proxy for Amqp/Mqtt over TCP ( [e3f9c27](https://github.com/Azure/iotedge/commit/e3f9c27a2613e0e5181b97225a42e4986207eaa9) )
* Added timeout sending messages to MQTT broker to prevent infinite waiting ( [0ce632f](https://github.com/Azure/iotedge/commit/0ce632f675d092c7867c0c77d3a1239a0abd29d0) )
* Improve EdgeHub config parsing ( [a59cfe2](https://github.com/Azure/iotedge/commit/a59cfe233c1ef7cc807d60a4678500ecb76ab282) )
* Set default auth mode to Scope ( [399a9a3](https://github.com/Azure/iotedge/commit/399a9a357e2dd98b03bd46ab81d303dd12bb0bdb) )

### Features
* Optimize Nested Edge descendents mapping ( [2cf92db](https://github.com/Azure/iotedge/commit/2cf92dbe26e5e256239613678f3b3f84f8b94935) )
* Support MQTT bridge events ( [a31eed4](https://github.com/Azure/iotedge/commit/a31eed41c60d8b18ecd3b524e3235a8978c219e1) )
* Handle thumbprint auth through API proxy for WebSocket ( [f710e43](https://github.com/Azure/iotedge/commit/f710e439afee72edc561486e2a0ef6d9d0994bfa) )
* Introduce unsubscription through nested levels on client-disconnect ( [eca18ee](https://github.com/Azure/iotedge/commit/eca18eec732409f80237026ee6a81e4dfc5c42b6) )


## API Proxy
### Bug Fixes
* 1.50 Rust toolchain update ( [040c54d](https://github.com/Azure/iotedge/commit/040c54dedf2f29a33c3302b468379b82c5dd3276) )
* Adding web sockect support for API proxy  ( [86ab1a0](https://github.com/Azure/iotedge/commit/86ab1a0ec158916410281744b7b64c005d6644e4) )
* Update Base Images for Security Vulnerability ( [6edad21](https://github.com/Azure/iotedge/commit/6edad2143d37af8a4213ffbe898e873faec36b58) )
* Fixing error message at API proxy start ( [4629a74](https://github.com/Azure/iotedge/commit/4629a749ef19adcca289eb573d1c2ec6e259bbbf) )
* Allow EdgeAgent image to be resolved via parent address ( [32834a6](https://github.com/Azure/iotedge/commit/32834a662a014365b26be13a86756368f2f3a5bf) )
* Change API proxy to accept a trustbundle with more than one root certificate ( [f1fc6d9](https://github.com/Azure/iotedge/commit/f1fc6d9c092985b4f5d395c5effce03f4e5cacd1) )

### Features
* Handle thumbprint auth through API proxy for WebSocket ( [f710e43](https://github.com/Azure/iotedge/commit/f710e439afee72edc561486e2a0ef6d9d0994bfa) )


## MQTT Broker
### Bug Fixes
* 1.50 Rust toolchain update ( [040c54d](https://github.com/Azure/iotedge/commit/040c54dedf2f29a33c3302b468379b82c5dd3276) )
* Persist in-flight queue on broker restart ( [6d81b94](https://github.com/Azure/iotedge/commit/6d81b94d413484e41243469e2430bff75ae514ee) )
* Send MQTT "will" on broker shutdown ( [3dced1c](https://github.com/Azure/iotedge/commit/3dced1ce9bfcc7fdb9b15348dbe6d6e152503175) )
* Retry non-iothub subscription when rejected by server ( [be3d482](https://github.com/Azure/iotedge/commit/be3d482d522394b00db740c127173c23549a31dc) )

### Features
* Introduced Ring Buffer to MQTT Bridge ( [354b04f](https://github.com/Azure/iotedge/commit/354b04fd2b29bf26702f1cf637bcb8357a98582d), [a24f6bb](https://github.com/Azure/iotedge/commit/a24f6bb27221a75993f92e1f40af49279ca77643), [e9d4d2b](https://github.com/Azure/iotedge/commit/e9d4d2b1f3f46389dacd97977cc8975635f67ab5), [c9d7ea3](https://github.com/Azure/iotedge/commit/c9d7ea3456b2b8c7893a050a5d1779d0f59a14de), [b86f014](https://github.com/Azure/iotedge/commit/b86f01494796cb7698d0c0eab9ed718755982c10), [8df7e88](https://github.com/Azure/iotedge/commit/8df7e8806712844d8e3ef53bcd41b01d1d37f09a), [2af9dac](https://github.com/Azure/iotedge/commit/2af9dacdc499f2f4326b5ef896d68df38ad3e43d), [6f12c88](https://github.com/Azure/iotedge/commit/6f12c88b6c1d9227f95e9f12c2c496083426415f), [a027808](https://github.com/Azure/iotedge/commit/a027808352d6c7f971d810235411a0fd956f4653), [7b35db9](https://github.com/Azure/iotedge/commit/7b35db925862162820999086c415879b264edbcc) )
* Configuration improvement ( [45bcc83](https://github.com/Azure/iotedge/commit/45bcc835b00b940923a99839de4548cdbbabc619), [a8223af](https://github.com/Azure/iotedge/commit/a8223af8c437ac5155e1c9b2b8341b734773ab5b) )


## aziot-edge
### Bug Fixes
* Improve `iotedge check` ( [6c7fc9b](https://github.com/Azure/iotedge/commit/6c7fc9b75360f34f9d66db6dc1d2656fa04fd83c), [e254d9c](https://github.com/Azure/iotedge/commit/e254d9cabbcb07ad7247bffa68082c4c6dd8b45c), [7245c8e](https://github.com/Azure/iotedge/commit/7245c8e053b97596dd05a6c70160d4569f77bee4), [455cef9](https://github.com/Azure/iotedge/commit/455cef92f2945cbe08c5b5e5abb39fe5311353ff) )
* Bugfix iotedge CLI ( [e254d9c](https://github.com/Azure/iotedge/commit/e254d9cabbcb07ad7247bffa68082c4c6dd8b45c), [c6a9bbb](https://github.com/Azure/iotedge/commit/c6a9bbb44737bac51c8945ed86bf776ef9a8279a) )
* Fix import of listen URIs when original config used socket activation ( [e4794ae](https://github.com/Azure/iotedge/commit/e4794aee0aa6c7d78bee83d62662d5935bf8f381) )
* Configuration changes for content trust with certificate service ( [9e2f4b8](https://github.com/Azure/iotedge/commit/9e2f4b8b03cc75358ad2a44dc6a213299115721e) )
* Retry getting device information on startup ( [492a159](https://github.com/Azure/iotedge/commit/492a15918aa2facaf130b7993ace663cd5ffe077) )
* Add iotedge-init-import command to migrate pre-1.2 config to 1.2+ config ( [e3bf3c9](https://github.com/Azure/iotedge/commit/e3bf3c9871bc9339253ac662eeefbcae43782287) )
* Temporarily raise quickstart EdgeCA cert's expiry to 30 days ( [9cc5b8f](https://github.com/Azure/iotedge/commit/9cc5b8f727bcd7de51fb021c8b231aadf8d4f381) )

### Features
* Convert iotedged config to TOML, and implement `iotedge config` ( [e254d9c](https://github.com/Azure/iotedge/commit/e254d9cabbcb07ad7247bffa68082c4c6dd8b45c), [d0978bf](https://github.com/Azure/iotedge/commit/d0978bf63bdd5624543680424452ee5c08fe285a) )
* Support Nested Edge topology ( [e254d9c](https://github.com/Azure/iotedge/commit/e254d9cabbcb07ad7247bffa68082c4c6dd8b45c), [dc7c929](https://github.com/Azure/iotedge/commit/dc7c92944beb3747c6f5341321025c9b541056f6) )
* Introduce `iotedge system` commands ( [cbe03af](https://github.com/Azure/iotedge/commit/cbe03af512105fcc3b8899a5aebce547cf924de9) )
* Make edgelet's certificate CA configurable ( [6073a78](https://github.com/Azure/iotedge/commit/6073a78a44af6dbdb78a28ac5b473b0b0b2d2874) )


# 1.2.0-rc3 (2020-12-22)
This is only container image update. We do not publish edgelet artifact in this release.
Please use the edgelet artifacts from release 1.2.0-rc1.

## Edge Agent
### Bug Fixes
* Add HostConfig properties ([503d51b](https://github.com/Azure/iotedge/commit/503d51b1627fc9e594b2f15277e1f987b2e44362))
* Update arm base images for security vulnerability ([07f6750](https://github.com/Azure/iotedge/commit/07f6750958063adc92f5c732fb6a8eca8a9a1dea))

### Features
* Update service SDK to 1.28.1-NestedEdge and and devices SDK to 1.33.1-NestedEdge ([858106f](https://github.com/Azure/iotedge/commit/858106ffcb90c435c396eef39fad29cd355de3cb))

## Edge Hub
### Bug Fixes
* Prevent stackflow when syncing circularly nested hierarchies ([bf58151](https://github.com/Azure/iotedge/commit/bf581517c0cb0d5861957dbf1567dc73a02e434f))
* Fix message count metrics ([c8d189b](https://github.com/Azure/iotedge/commit/c8d189b29b90c9c9adec337c5b00ea141685e2bf))
* Policy engine fixes ([21dfb49](https://github.com/Azure/iotedge/commit/21dfb49a35dc6c50044221c8ff8ac2a7844d0ab1))
* Update arm base images for security vulnerability ([07f6750](https://github.com/Azure/iotedge/commit/07f6750958063adc92f5c732fb6a8eca8a9a1dea))
* Fix bug with MQTT Bridge prefixes ([f9cf9a3](https://github.com/Azure/iotedge/commit/f9cf9a3772feafa63db3a5d2c9ebd12840eee996))
* Treat initial container connection state as disconnected ([ff92b28](https://github.com/Azure/iotedge/commit/ff92b28adb0d1c45f69fe2d770f65cf6372e4b7f))
* Lock down MQTT Broker environment variables ([36175e1](https://github.com/Azure/iotedge/commit/36175e1c5952c7ab99f103eaa7489de5eca996c3))
* MQTT Bridge remove all sub when upstream bridge is missing from configuration update ([ba655e3](https://github.com/Azure/iotedge/commit/ba655e3e0c29a47275a6b59381b875d010a79844))
* EdgeHub awaits for Twin in non MQTT Broker scenario ([ee0b87f](https://github.com/Azure/iotedge/commit/ee0b87f5cee30db6dc470b45f127eb394c2d1db2))

### Features
* Enable PnP for MQTT Broker ([baf74c8](https://github.com/Azure/iotedge/commit/baf74c8d01c0e04896596492fe6f0ad0a1b40bd8))
* Add message cleanup interval environment variable ([dbcc2d9](https://github.com/Azure/iotedge/commit/dbcc2d91afeca8a21ba170f6ae595b1f4d1cd645))
* Update service SDK to 1.28.1-NestedEdge and and devices SDK to 1.33.1-NestedEdge ([858106f](https://github.com/Azure/iotedge/commit/858106ffcb90c435c396eef39fad29cd355de3cb))
* Support for sha256 thumbprint authentication ([dac5710](https://github.com/Azure/iotedge/commit/dac5710d79b034031ead89c14a66feb04ed5d8a7))
* Add client connection related EdgeHub metrics ([c7da97b](https://github.com/Azure/iotedge/commit/c7da97bf1a2df6b7995613631dc8258cd8b6aa4b))

## Other Module Images
### Bug Fixes
* Update arm base images for security vulnerability ([07f6750](https://github.com/Azure/iotedge/commit/07f6750958063adc92f5c732fb6a8eca8a9a1dea))

# 1.2.0-rc2 (2020-11-20)
This is only container image update. We do not publish edgelet artifact in this release.
Please use the edgelet artifacts from release 1.2.0-rc1.

## Edge Agent
### Bug Fixes
* Connect to parent IoT Edge device in a hierarchical configuration. ([b92785c](https://github.com/Azure/iotedge/commit/b92785c2fa1b123daf3f9a21b5f7c2f4110f9b19))
* Fix vulnerability issues in ARM-based docker images ([383aee3](https://github.com/Azure/iotedge/commit/383aee305aed93fd82bde1224d9843914612882d))

## Edge Hub
### Bug Fixes
* Improve M2M feedback handling ([b1eceeb](https://github.com/Azure/iotedge/commit/b1eceebc09ef0c696baee60926b31de3abc55f2f))
* Fix Policy Engine issue where not all variable rules are evaluated ([d83850c](https://github.com/Azure/iotedge/commit/d83850c6261444006e2aa091909eedf1fcfeb8b6))
* Use fully qualified name for the authenticated identity on MQTT broker ([cfed086](https://github.com/Azure/iotedge/commit/cfed086299f4cd716d8ab49c6e09847b5c45c828))
* Fix vulnerability issues in ARM-based docker images ([383aee3](https://github.com/Azure/iotedge/commit/383aee305aed93fd82bde1224d9843914612882d))

## Other modules
### Bug Fixes
* Apply proxy setting only for http protocol ([dd8b529](https://github.com/Azure/iotedge/commit/dd8b529d67fcc0fc5adaa92ecf4d1758dfed4eaf))
* Fix vulnerability issues in ARM-based docker images ([383aee3](https://github.com/Azure/iotedge/commit/383aee305aed93fd82bde1224d9843914612882d))


# 1.2.0-rc1 (2020-11-09)
* Preview support for nesting IoT Edge devices in gateway configuration, to allow creation of hierarchies of IoT Edge devices.
* Preview support for MQTT 3.1.1 compliant broker in EdgeHub.
* Updates to `iotedge check` troubleshooting command to work in hierarchical configuration.

## Edge Agent
### Features
* Connect to parent IoT Edge device in a hierarchical configuration. ([b92785c](https://github.com/Azure/iotedge/commit/b92785c2fa1b123daf3f9a21b5f7c2f4110f9b19))
* Upload module logs and support bundle in a hierarchical configuration. ([37e8d08](https://github.com/Azure/iotedge/commit/37e8d08ba0af6571f629b7606c518dcc24e81ca6))
* Pull docker container images in a hierarchical configuration ([e82200d](https://github.com/Azure/iotedge/commit/e82200d31ad5745e7a8cb75abd99005ff314bede))

## Edge Hub
### Features
* Authenticate clients in a hierarchical configuration, including child IoT Edge devices that may be connecting on behalf of their children ([32a4e06](https://github.com/Azure/iotedge/commit/32a4e06ee0e4538210caa551169970c17f61bde0))
* Added module creation APIs to allow child IoT Edge devices to create modules in IoT Hub ([6336d92](https://github.com/Azure/iotedge/commit/6336d9209a56716fdcda29722e4c2bed451029a8))
* MQTT 3.1.1 compliant broker ([eb4a8cb](https://github.com/Azure/iotedge/commit/eb4a8cb9554ac48bc5c7954853cf4b211e4e37a2))
* Support for setting authorization policies for custom MQTT topics ([5effde9](https://github.com/Azure/iotedge/commit/5effde9b6d261cd1368943138191850a2ff0d465))
* Support for bridging MQTT topics to MQTT Broker in parent IoT Edge device ([5a79646](https://github.com/Azure/iotedge/commit/5a796463c97c3831990ed5aba58d06ee79049ca7))

## iotedged
### Features
* Configuration updates to support nesting IoT Edge devices ([b92785c](https://github.com/Azure/iotedge/commit/b92785c2fa1b123daf3f9a21b5f7c2f4110f9b19))
* Updates to `iotedge check` troubleshooting command to work in hierarchical configuration ([c0bad52](https://github.com/Azure/iotedge/commit/c0bad527da979fc0d8d1c810474e5078dfee83ca), [24b1c78](https://github.com/Azure/iotedge/commit/24b1c78f835068de7795f960660d45a889b4ae1b))

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
