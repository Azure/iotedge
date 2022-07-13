# 1.1.15 (2022-07-12)
## Edge Agent
### Bug Fixes
* Update base image to include .NET reliability and non-security fixes from [.NET Core 3.1.27 - July 12, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.27/3.1.27.md)
* Update base image to include .NET security fixes from [.NET Core 3.1.26 - June 14, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.26/3.1.26.md)


## Edge Hub
### Bug Fixes
* Update base image to include .NET reliability and non-security fixes from [.NET Core 3.1.27 - July 12, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.27/3.1.27.md)
* Update base image to include .NET security fixes from [.NET Core 3.1.26 - June 14, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.26/3.1.26.md)


## Diagnostics Module
### Bug Fixes
* Update base image to include .NET reliability and non-security fixes from [.NET Core 3.1.27 - July 12, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.27/3.1.27.md)
* Update base image to include .NET security fixes from [.NET Core 3.1.26 - June 14, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.26/3.1.26.md)


## Simulated Temperature Sensor
### Bug Fixes
* Update base image to include .NET reliability and non-security fixes from [.NET Core 3.1.27 - July 12, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.27/3.1.27.md)
* Update base image to include .NET security fixes from [.NET Core 3.1.26 - June 14, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.26/3.1.26.md)


## iotedged
### Bug Fixes
* Create a single persistent workload socket for edgeAgent [35f8781](https://github.com/Azure/iotedge/commit/35f87818de7c1d1fd4925606119b9970267d9abe)


# 1.1.14 (2022-05-24)
## Edge Agent
### Bug Fixes
* Update Base Images to address Microsoft .NET Security Updates for [CVE-2022-23267](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-23267), [CVE-2022-29117](https://github.com/dotnet/announcements/issues/220), [CVE-2022-29145](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-29145), OpenSSL vulnerability [USN-5402-1](https://ubuntu.com/security/notices/USN-5402-1), curl vulnerability [USN-5412-1](https://ubuntu.com/security/notices/USN-5412-1), and OpenLDAP Vulnerability [USN-5424-1](https://ubuntu.com/security/notices/USN-5424-1)


## Edge Hub
### Bug Fixes
* Update Base Images to address Microsoft .NET Security Updates for [CVE-2022-23267](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-23267), [CVE-2022-29117](https://github.com/dotnet/announcements/issues/220), [CVE-2022-29145](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-29145), OpenSSL vulnerability [USN-5402-1](https://ubuntu.com/security/notices/USN-5402-1), curl vulnerability [USN-5412-1](https://ubuntu.com/security/notices/USN-5412-1), and OpenLDAP Vulnerability [USN-5424-1](https://ubuntu.com/security/notices/USN-5424-1)


## Diagnostics Module
### Bug Fixes
* Update Base Images to address Microsoft .NET Security Updates for [CVE-2022-23267](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-23267), [CVE-2022-29117](https://github.com/dotnet/announcements/issues/220), [CVE-2022-29145](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-29145), OpenSSL vulnerability [USN-5402-1](https://ubuntu.com/security/notices/USN-5402-1), curl vulnerability [USN-5412-1](https://ubuntu.com/security/notices/USN-5412-1), and OpenLDAP Vulnerability [USN-5424-1](https://ubuntu.com/security/notices/USN-5424-1)


## Simulated Temperature Sensor
### Bug Fixes
* Update Base Images to address Microsoft .NET Security Updates for [CVE-2022-23267](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-23267), [CVE-2022-29117](https://github.com/dotnet/announcements/issues/220), [CVE-2022-29145](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-29145), OpenSSL vulnerability [USN-5402-1](https://ubuntu.com/security/notices/USN-5402-1), curl vulnerability [USN-5412-1](https://ubuntu.com/security/notices/USN-5412-1), and OpenLDAP Vulnerability [USN-5424-1](https://ubuntu.com/security/notices/USN-5424-1)


# 1.1.13 (2022-04-27)
## Edge Agent
### Bug Fixes
* Update Base Images to address gzip vulnerability [CVE-2022-1271](https://ubuntu.com/security/CVE-2022-1271)


## Edge Hub
### Bug Fixes
* Update Base Images to address gzip vulnerability [CVE-2022-1271](https://ubuntu.com/security/CVE-2022-1271)


## iotedged
### Bug Fixes
* Update Rust `regex` version to address vulnerability [5573eff](https://github.com/Azure/iotedge/commit/5573eff191310e5a226d924b40d1cea035b0a3fd)


## Diagnostics Module
### Bug Fixes
* Update Base Images to address gzip vulnerability [CVE-2022-1271](https://ubuntu.com/security/CVE-2022-1271)


## Simulated Temperature Sensor
### Bug Fixes
* Update Base Images to address gzip vulnerability [CVE-2022-1271](https://ubuntu.com/security/CVE-2022-1271)


# 1.1.12 (2022-03-15)
## Edge Agent
### Bug Fixes
* Update Base Images for a Security Patch [.NET Core 3.1.23 - March 8, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.23/3.1.23.md)


## Edge Hub
### Bug Fixes
* Update Base Images for a Security Patch [.NET Core 3.1.23 - March 8, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.23/3.1.23.md)


# 1.1.11 (2022-03-07)
## Edge Agent
### Bug Fixes
* Remove `BouncyCastle` Dependency [f18f057](https://github.com/Azure/iotedge/commit/f18f057013367e8fbdf24b30c2529c7e1149a691)
* Remove k8s Projects [ad08af1](https://github.com/Azure/iotedge/commit/ad08af1693dfa1f8185f351a4dfdb05e1a30f550)
* Update Base Images for a Security Patch [2395776](https://github.com/Azure/iotedge/commit/23957760bad8ce8dcbbabb97994a2618eff38fa3)
* Update `DotNetty.Common` Version [2a76f57](https://github.com/Azure/iotedge/commit/2a76f57cb60922a99472b42fe7b4d8659baf114e)


## Edge Hub
### Bug Fixes
* Remove `BouncyCastle` Dependency [f18f057](https://github.com/Azure/iotedge/commit/f18f057013367e8fbdf24b30c2529c7e1149a691)
* Update Base Images for a Security Patch [2395776](https://github.com/Azure/iotedge/commit/23957760bad8ce8dcbbabb97994a2618eff38fa3)
* Improve Certifacte Import Logging.[c9f8daa](https://github.com/Azure/iotedge/commit/c9f8daa3599a51997e5209893ab881944bd2cb8f)
* Workaround Windows-certificate import issue [4eb2bdd](https://github.com/Azure/iotedge/commit/4eb2bddeb60cfee9b98d64a29f7813efa9f94fb3) [cf22777](https://github.com/Azure/iotedge/commit/cf22777e5381cac91980c926de30474ea396e6ec)
* Fix EdgeHub does not Restart to Renew Ceritificate due to MqttProtocolHead Failed to Close [9339550](https://github.com/Azure/iotedge/commit/9339550b5b267f56f0d198e85cb80e6ab08d7532)
* Update `DotNetty.Common` Version [2a76f57](https://github.com/Azure/iotedge/commit/2a76f57cb60922a99472b42fe7b4d8659baf114e)


## iotedged
### Bug Fixes
* Update `Regex` Vulnerability [9f56fd2](https://github.com/Azure/iotedge/commit/9f56fd2b03ad1caa8a5b7e8fd10e77cdd9156b5e)
* Update `tokio`, `rayon`, and `crossbeam` [dc30b65](https://github.com/Azure/iotedge/commit/dc30b650c869595c0a028f4376dd5315eb571830)
* Replace `rand()` for Serial Numbers of New Certificates with OpenSsl's `RAND_bytes` [e8d8240](https://github.com/Azure/iotedge/commit/e8d8240253fd445f54dfd2c422d2a0a47761e8a9)


# 1.1.10 (2022-02-03)
## Edge Agent
### Bug Fixes
* Update Base Images for a Security Patch [0ac3602](https://github.com/Azure/iotedge/commit/0ac3602f2fa5dd08b20826f68394a59f9434a691)


## Edge Hub
### Bug Fixes
* Update Base Images for a Security Patch [0ac3602](https://github.com/Azure/iotedge/commit/0ac3602f2fa5dd08b20826f68394a59f9434a691)


# 1.1.9 (2022-01-28)
## iotedged
### Bug Fixes
* Removing moby check for non-windows [b71b828](https://github.com/Azure/iotedge/commit/b71b828f0304dace8c2c1225e8c29eced9ab285c)


## Edge Agent
### Bug Fixes
* Fix for concurrent module creation with workload socket [1a30568](https://github.com/Azure/iotedge/commit/1a30568e55790011919295e23938f5b1cef2dc14)
* Update Base Images for a Security Patch [7b689b3](https://github.com/Azure/iotedge/commit/7b689b3d58f2b732f7ff5138ba36c9c331449322)


## Edge Hub
### Bug Fixes
* Fix for Edge hub queue length counting metric [32fbcfe](https://github.com/Azure/iotedge/commit/32fbcfe8e477b5c593bba5660fd8044031703742)
* Update Base Images for a Security Patch [7b689b3](https://github.com/Azure/iotedge/commit/7b689b3d58f2b732f7ff5138ba36c9c331449322)


# 1.1.8 (2021-11-02)
## iotedged
### Bug Fixes
* Fix permission denied on workload socket [53157f2](https://github.com/Azure/iotedge/commit/53157f28c55eae5058527d87e6555eab863927e4)
* IoTedge Check checks expired production certs [1baad81](https://github.com/Azure/iotedge/commit/1baad8145781bac27306ed585f23a7af0742c370)


## Edge Agent
### Bug Fixes
* Recreate edgeAgent when not Running, Stopped, or Failed [9efa300](https://github.com/Azure/iotedge/commit/9efa30091697c9617189b08b495fae8299f00787)


## Edge Hub
### Bug Fixes
* Remove WebSocket Ping KeepAlives [a346b3b](https://github.com/Azure/iotedge/commit/a346b3bf3bca5398022ba1de911ac14fbdc7b1d0)
* Detect fail-over from iot hub/sdk behavior and disconnect from hub [01cd351](https://github.com/Azure/iotedge/commit/01cd3510c54c71010845a4b770cd9d5be04719f7)
* Correct wrong path for MessageStore.cs [6c8906d](https://github.com/Azure/iotedge/commit/6c8906d468be98b37b4db60a1361682469095baa)
* Update dependency on vulnerable package [9ae0ddf](https://github.com/Azure/iotedge/commit/9ae0ddfb7b6fc5186d7c9107db2f6415819d8afe)


# 1.1.7 (2021-10-13)
## iotedged
### Bug Fixes
* Introduce `allow_elevated_docker_permissions` option in daemon config [6725a11](https://github.com/Azure/iotedge/commit/6725a11dc6a557478d9fe11820847e75f4ab27a1)
* Fix a container socket stops working after a module restart [2e6b208](https://github.com/Azure/iotedge/commit/2e6b2082e5a632e6d041d95c625a669b8daab2b4)


## Edge Agent
### Bug Fixes
* Create parent socket folder in Windows upgrade script [6d0ca55](https://github.com/Azure/iotedge/commit/6d0ca554467300144395aa3715b1140335d3b9c2), [7ed543a](https://github.com/Azure/iotedge/commit/7ed543afb33d9a8424800ed094269c5b029df5d2)
* Update Base Images for a Security Patch [75bb5ea](https://github.com/Azure/iotedge/commit/75bb5eabb8c243d56ad3477b79b429060c7cb2d3)


## Edge Hub
### Bug Fixes
* Update Base Images for a Security Patch [75bb5ea](https://github.com/Azure/iotedge/commit/75bb5eabb8c243d56ad3477b79b429060c7cb2d3)


# 1.1.6 (2021-09-06)
## iotedged
### Bug Fixes
* Hotfix the container socket failure upon a module deployment on Windows [382cf77](https://github.com/Azure/iotedge/commit/382cf778bf061cf02ca893a68584e63eb6c6c878)


# 1.1.5 (2021-08-27)
## Edge Agent
### Bug Fixes
* Fix twin pulls on reconnection between two edge devices with the same identity [c7a81c1](https://github.com/Azure/iotedge/commit/c7a81c193b901d5799f8a782b36290f8da8d6cd6)
* Update package dependency for security vulnerabilities [e7c900d](https://github.com/Azure/iotedge/commit/e7c900deb4573c7cf51e1c62c59971224e7999cd), [c347dba](https://github.com/Azure/iotedge/commit/c347dbae914d081b1994bb5da2db2efe645b5d99)
* Update base images for a security patch [0998c04](https://github.com/Azure/iotedge/commit/0998c04f9ce71fbe9ea56bd7453316fae42ec2e9)
* Update Prometheus versions to 4.2.0 [842c8c3](https://github.com/Azure/iotedge/commit/842c8c3d56e7ebc77aefd25a40bc38ffa96ba118)


## Edge Hub
### Bug Fixes
* Update package dependency for security vulnerabilities [e7c900d](https://github.com/Azure/iotedge/commit/e7c900deb4573c7cf51e1c62c59971224e7999cd), [c347dba](https://github.com/Azure/iotedge/commit/c347dbae914d081b1994bb5da2db2efe645b5d99)
* Update base images for a security patch [0998c04](https://github.com/Azure/iotedge/commit/0998c04f9ce71fbe9ea56bd7453316fae42ec2e9)
* Decouple pooled buffers functionality from `OptimizeForPerformance` flag [50027ff](https://github.com/Azure/iotedge/commit/50027ff39e22e45335c5ce5f30e0fda23d8f26be)
* Update Prometheus versions to 4.2.0 [842c8c3](https://github.com/Azure/iotedge/commit/842c8c3d56e7ebc77aefd25a40bc38ffa96ba118)


## Azure Functions Module Sample
### Bug Fixes
* Update Azure Functions packages [58cfa2b](https://github.com/Azure/iotedge/commit/58cfa2b8cea5811ff3f1a61441fd001ee6b25d69)
* Update base images for a security patch [0998c04](https://github.com/Azure/iotedge/commit/0998c04f9ce71fbe9ea56bd7453316fae42ec2e9)


## iotedged
### Bug Fixes
* Fix Rust security vulnerabilities [31b488f](https://github.com/Azure/iotedge/commit/31b488f33acbb3eeac6cdf86422b1c2785f3b92c), [8bd4fff](https://github.com/Azure/iotedge/commit/8bd4fffbb0eb5c2f2291fda4263d6c6b693571e0)
* Organize Unix and Workload socket on containers. [b7a6d10](https://github.com/Azure/iotedge/commit/b7a6d10721943012ecde657ac677cf356f5139e1)


# 1.1.4 (2021-07-05)
## Edge Agent
### Bug Fixes
* Use Docker Timestamp When Log Timestamp is not Available in JSON log. [b4d989b](https://github.com/Azure/iotedge/commit/b4d989bb9d0a92ac9d83c8d1be9384c6bcd61530)
* Update Base Images for a Security Patch. [de48c49](https://github.com/Azure/iotedge/commit/de48c495bd182a2fc943c52d3b3e80eb8bebe0cf)


## Edge Hub
### Bug Fixes
* Add validation for null props inside objects inside arrays. [a6d7fee](https://github.com/Azure/iotedge/commit/a6d7feec065037a5d6f514e7c3b3dcdc6108a20c)
* Fixed subscription restore from client state when mqtt client reconnects. [aadf030](https://github.com/Azure/iotedge/commit/aadf03037ef17927c9d69b870a21800a6bcd73e0)
* Send connection device Id information on twin change notifications. [acc3e1f](https://github.com/Azure/iotedge/commit/acc3e1f711a7e0c0f9a88a09af79d33885025d26)
* Update Base Images for a Security Patch. [de48c49](https://github.com/Azure/iotedge/commit/de48c495bd182a2fc943c52d3b3e80eb8bebe0cf)


## Temperature Filter Function
### Bug Fixes
* Update to use Azure Functions 3.0 [0d18c66](https://github.com/Azure/iotedge/commit/0d18c661bc0b50186bf51743e2b7d974282c7674)


# 1.1.3 (2021-05-24)
## Edge Agent
### Bug Fixes
* Update GetModuleLogs method when tail + since + until options are provided. [c1dba55](https://github.com/Azure/iotedge/commit/c1dba55ece869932b5a75ae0de658121dd75e54f)
* Fix vulnerability issues in docker images. [b286408](https://github.com/Azure/iotedge/commit/b28640847c7d8efd82244d0628a36c63e20e5473)


## Edge Hub
### Bug Fixes
* Adds SharedAccessSignature to repo with fix for vulnerability. [4fea6e7](https://github.com/Azure/iotedge/commit/4fea6e7750984e278e20e27635eea223c68c1e3f)
* Close AMQP connection explicitly when no more links (removing links kept tcp level connection). [5be30fb](https://github.com/Azure/iotedge/commit/5be30fb80a69fb36a8de08541568c2d1f161fe2b)
* Fix vulnerability issues in docker images. [b286408](https://github.com/Azure/iotedge/commit/b28640847c7d8efd82244d0628a36c63e20e5473)
* Fix edgehub_queue_length metric. [4aab90b](https://github.com/Azure/iotedge/commit/4aab90b0aeab80ee30d1c7984a930e643bf39b65)


## Diagnostics Module
### Bug Fixes
* Fix potential instability in iotedged after UploadSupportBundle fails. [5e3b60f](https://github.com/Azure/iotedge/commit/5e3b60f8cdd47dc1374a040ed3eecef0824b9c04)
* Fix vulnerability issues in docker images. [b286408](https://github.com/Azure/iotedge/commit/b28640847c7d8efd82244d0628a36c63e20e5473)
* Fix Diagnostics Module's Websocket behind proxy. [6b32f3c](https://github.com/Azure/iotedge/commit/6b32f3c7b3f492aa12e418013844e46540bed9d5)

## iotedged
### Changes
* Update C SDK submodules to 2020-12-09. [deb2753](https://github.com/Azure/iotedge/commit/deb2753bc2e36bf2355ddb9427262e5d93d96d72)
* Delete all containers after reprovision. [77ad781](https://github.com/Azure/iotedge/commit/77ad7813b2b5aaaa2f4e96bf7333dcb9dd912766)
* Update serde-yaml version to 0.8 [22b8d30](https://github.com/Azure/iotedge/commit/22b8d3012a700609ae4b1ab5cca378e68a8899e9)
* Introduce Timestamps Option via mgmt.sock. [b18d090](https://github.com/Azure/iotedge/commit/b18d09056305a911553f17a9b64cbe316696e208)


# 1.1.2 (2021-04-15)
## Edge Agent
### Bug Fixes
* Fix vulnerability issues in docker images [fdbad67](https://github.com/Azure/iotedge/commit/fdbad674c8b0b30d9d58a2160ad7199818987416)


## Edge Hub
### Bug Fixes
* Fix vulnerability issues in docker images [fdbad67](https://github.com/Azure/iotedge/commit/fdbad674c8b0b30d9d58a2160ad7199818987416)


# 1.1.1 (2021-03-18)
## Edge Agent
### Bug Fixes
* Fix vulnerability issues in docker images [694bc54](https://github.com/Azure/iotedge/commit/694bc5433f9d718f61292d812eb648ffb328b145)


## Edge Hub
### Bug Fixes
* Sending messages to cloud is blocked when a leaf device is disabled [756b83b](https://github.com/Azure/iotedge/commit/756b83b9062624a3d0c2e55f59a56195cc6704b8)
* Fix vulnerability issues in docker images [694bc54](https://github.com/Azure/iotedge/commit/694bc5433f9d718f61292d812eb648ffb328b145)


## iotedged
### Changes
* Make comments in the config.yaml for provisioning and certificates clearer [771be81](https://github.com/Azure/iotedge/commit/771be81fceaf0a585dd9d7158e422dc20a0227b9) [8cb3c2b](https://github.com/Azure/iotedge/commit/8cb3c2b2864dd57e926f08b06a689475717868dc)
* Upgrade sysinfo package to 0.14.10 [b53cc33](https://github.com/Azure/iotedge/commit/b53cc33a20690008a4f5d84dd448caf14718eefa)
* Update default agent tag in config.yaml to 1.1 [e4c6eae](https://github.com/Azure/iotedge/commit/e4c6eae4120ac47b3fbecaf173e129520bffacda)


# 1.1.0 (2021-02-10)
## Change to Supported Systems
* **Remove support for Ubuntu 16.04**. Ubuntu will soon end their support for 16.04, so we're changing our support to match. Ubuntu 18.04 continues to be supported.
## Edge Agent
### Bug Fixes
* Fix `since` parameter in `GetModuleLogs` direct method [8d9a8e0](https://github.com/Azure/iotedge/commit/8d9a8e0eff2b47b99a4bfb28af2d3501f901c8af)
* Don't pass HTTPS proxy information to the cloud connection for protocols that don't use port 443 [ca2fa42](https://github.com/Azure/iotedge/commit/ca2fa428e3c61fc53ce4d9a58d4d6094e51c4e5c)
* Update config version even when plan is empty [97532d0](https://github.com/Azure/iotedge/commit/97532d05f8ec0777dc41290dc25b2cee0813b66e)
* Fix vulnerability issues in docker images [4dbaa62](https://github.com/Azure/iotedge/commit/4dbaa6207e8e899fdd50dfd3a3b031713964bdb6), [3c569ac](https://github.com/Azure/iotedge/commit/3c569ac868b584cbe048447c6783a5fc93985082)


## Edge Hub
### Changes
* **Edge Hub allows only child devices to connect by default**. To connect a leaf device to the Edge Hub, users must [establish a parent/child relationship](https://docs.microsoft.com/en-us/azure/iot-edge/offline-capabilities?view=iotedge-2018-06#set-up-parent-and-child-devices) between the edge device and the leaf device. In previous versions, this was required only for offline scenarios or when using certificate-based authentication. For online scenarios Edge Hub could fall back to cloud-based authentication for leaf devices that were using SAS key-based authentication. With this change, leaf devices with SAS key-based authentication need to be a children of the edge device. You can configure Edge Hub to go back to the previous behavior by setting the environment variable "AuthenticationMode" to the value "CloudAndScope".
### Bug Fixes
* Continue message store cleanup after encountering db error [4a196f0](https://github.com/Azure/iotedge/commit/4a196f0b4a2f04f9bd8988fdea4c3f308fd67546)
* Don't pass HTTPS proxy information to the cloud connection for protocols that don't use port 443 [ca2fa42](https://github.com/Azure/iotedge/commit/ca2fa428e3c61fc53ce4d9a58d4d6094e51c4e5c)
* Fix vulnerability issues in docker images [4dbaa62](https://github.com/Azure/iotedge/commit/4dbaa6207e8e899fdd50dfd3a3b031713964bdb6), [3c569ac](https://github.com/Azure/iotedge/commit/3c569ac868b584cbe048447c6783a5fc93985082)

# 1.0.10.4 (2020-12-18)
## Edge Agent
### Bug Fixes
* Rebase Edge on K8s [c94bc73](https://github.com/Azure/iotedge/commit/c94bc733c05d91acfd77c87dcaf6b701601985fb)
* Fix vulnerability issues in ARM-based docker images [6603778](https://github.com/Azure/iotedge/commit/6603778d2c4dbb63aea4627f66feb5920eede3c6)
* GetModuleLogs works with http endpoints [cf176be](https://github.com/Azure/iotedge/commit/cf176be66bab28b032f0069a445ed21cc9fcdfb9)

## Edge Hub
### Features
* Introduce new metrics [40b2de9](https://github.com/Azure/iotedge/commit/40b2de93787140c52c8baee36860a6eb833e03d5)

### Bug Fixes
* Rebase Edge on K8s [c94bc73](https://github.com/Azure/iotedge/commit/c94bc733c05d91acfd77c87dcaf6b701601985fb)
* Support SHA256-based for thumbprint authentication [a88c72e](https://github.com/Azure/iotedge/commit/a88c72ebbfbd2ed88c8f6c8b41060b19e129c9d9)
* Fix `edgehub_queue_length` metric [2861dbc](https://github.com/Azure/iotedge/commit/2861dbc63757787ef52897d9acc732430b46313b)
* Fix vulnerability issues in Linux ARM and Windows AMD docker images [73fa197](https://github.com/Azure/iotedge/commit/73fa1976367559d38b6392f86e625906da1117bd)

## iotedged
### Bug Fixes
* Rebase Edge on K8s [c94bc73](https://github.com/Azure/iotedge/commit/c94bc733c05d91acfd77c87dcaf6b701601985fb)

# 1.0.10.3 (2020-11-18)
## Edge Agent
### Bug Fixes
* Fix vulnerability issues in ARM-based docker images [6603778](https://github.com/Azure/iotedge/commit/6603778d2c4dbb63aea4627f66feb5920eede3c6)

## Edge Hub
### Bug Fixes
* Fix vulnerability issues in ARM-based docker images [6603778](https://github.com/Azure/iotedge/commit/6603778d2c4dbb63aea4627f66feb5920eede3c6)

# 1.0.10.2 (2020-11-11)
## iotedged
### Bug Fixes
* Recognize the new DPS registration substatus that indicates cloud identity did not change [2321509](https://github.com/Azure/iotedge/commit/23215094cdd11fd5170d42798ebac509b132a8fd)
* Fix an error in the management endpoint URL built by the diagnostics module ('connect-management-uri' check) [dfa0967](https://github.com/Azure/iotedge/commit/dfa0967ade21d49d4059ca8a1dc602e80b5d22a2)

# 1.0.10.1 (2020-11-04)
## Edge Agent
### Bug Fixes
* Eliminate redundant calls to get a module's twin [286f15b](https://github.com/Azure/iotedge/commit/286f15bec93f745e2f8325908c696417d0891c5b)
* Fix a problem with batch metrics upload [ab8de09](https://github.com/Azure/iotedge/commit/ab8de098cec64f28eba1f53fe5fbcf387add9795)
* Fix vulnerability issues in ARM-based docker images [b8bc9f0](https://github.com/Azure/iotedge/commit/b8bc9f08a2bcc8b4ddc8bf2bf69ba6a2c8b4e87d)

## Edge Hub
### Bug Fixes
* Fix vulnerability issues in ARM-based docker images [b8bc9f0](https://github.com/Azure/iotedge/commit/b8bc9f08a2bcc8b4ddc8bf2bf69ba6a2c8b4e87d)

## iotedged
### Bug Fixes
* Fix DPS-X509 provisioning behind a proxy to use the client certificate for the proxied connection [0153812](https://github.com/Azure/iotedge/commit/0153812ae1a5e4a2c0622f3a2f38d67d8102bf2b)
* Don't fail `iotedge support-bundle` if iotedged is not running [93d1234](https://github.com/Azure/iotedge/commit/93d1234f01e26ecd14ebb3b9fb674e458a834d7b)
* Fix problems in the diagnostics module used by `iotedge check` [4257b87](https://github.com/Azure/iotedge/commit/4257b87de0f4ca59eb894a5924fce743d03e24d6)

# 1.0.10 (2020-10-12)
## Edge Agent
### Features
* Disable deployment manifest minor version validation [4a4f880](https://github.com/Azure/iotedge/commit/4a4f880e800620c3c8b0e330a29252a54cf51496)
* Add the following metrics {provisioning type, and virtualized environment} [e2ed141](https://github.com/Azure/iotedge/commit/e2ed141b569be5e4bd42c339b98ca7109ae4b940) [be747cc](https://github.com/Azure/iotedge/commit/be747cc0429f7c22df4c6d484347f62837aeb9b7)
* Allow scientific notation and escaped quotes inside Prometheus metric label [9c3d211](https://github.com/Azure/iotedge/commit/9c3d21146d1fbf5254310b7f97dcffbbe2a51f2a)
* Enable `MetricsHistogramMaxAge` [c550463](https://github.com/Azure/iotedge/commit/c550463919e0994ac2a977fe3cca66e02959d8ce) [c958739](https://github.com/Azure/iotedge/commit/c958739a18a1a9a1213ce8c22e3fa7823fba2dfd)
* Make Histogram quantiles { 0.1, 0.5, 0.9 and 0.99 } [c550463](https://github.com/Azure/iotedge/commit/c550463919e0994ac2a977fe3cca66e02959d8ce) [64d488e](https://github.com/Azure/iotedge/commit/64d488ece9065dbfdd857ab47ba9b57054462da0)
* Aggregate metrics before upload [c456806](https://github.com/Azure/iotedge/commit/c4568069a27d778c1b2a623604e2b79d7bf12fbe) [fadf5fa](https://github.com/Azure/iotedge/commit/fadf5fa1f6571c0898dfdb83e56887b5a287bde7) 
* Allows Agent to run as non-root in Linux, and as `ContainerUser` in Windows [3ce2fa5](https://github.com/Azure/iotedge/commit/3ce2fa5cfbb3eda1ec22165199c572afd9b4d0e4)
* Ability to remotely get support-bundle via edge agent direct method [b0a872a](https://github.com/Azure/iotedge/commit/b0a872aeca22865d2f1d558122aa62057d97669a) [186ff12](https://github.com/Azure/iotedge/commit/186ff12105bc360f21de6858fe750579475fe5f6)
* Edge agent periodically sends product quality telemetry. You can opt-out by setting the environment variable `SendRuntimeQualityTelemetry` to `false` for the edge agent. [f379462](https://github.com/Azure/iotedge/commit/f379462c1bb10caea0b17c15befcab0f410f1480)
* Edge agent now hash all instances of module ids in device telemetry. [46f40fc](https://github.com/Azure/iotedge/commit/46f40fcaccebcd5b9eb4fc006e30cd4db5ff22e4)
* Rename log upload method from `UploadLogs` to `UploadModuleLogs`  [b567801](https://github.com/Azure/iotedge/commit/b5678011925d0b8ee2eee716a9b7264863608e03)
* Rename reboot order from `priority` to be `startupOrder` [eed9c06](https://github.com/Azure/iotedge/commit/eed9c064ae0ad5342a2196bd42feff2d8b7e9cdc)
* Update SDK version {Microsoft.Azure.Devices.Client.1.28.0}. [cdf36b0](https://github.com/Azure/iotedge/commit/cdf36b01a7a18b0a4f9a1e3d5943fb9aa44029ea)
* Update codebase to dotnet 3.1. [f87a18a](https://github.com/Azure/iotedge/commit/f87a18a487ea0c05752254aaba04a4f89028120a)
* Install Trust Bundle. [4f85dcc](https://github.com/Azure/iotedge/commit/4f85dcc7d3fd6d4772d4a9b86ec3ecad651938fd)
* Add metrics upload to IoTHub feature [eff5c85](https://github.com/Azure/iotedge/commit/eff5c859e101d14eda85b80513136a2f3a473892)
* Add "Cmd", "Entrypoint", and "WorkingDir" translations for Kubernetes. [7cbc607](https://github.com/Azure/iotedge/commit/7cbc607ccc483d6e0ab9642be76c8b2d8bc09605)
* Add Experimental k8s create option feature for pod security context, resources, volumes, nodeSelector, and strategy. [cf2eba9](https://github.com/Azure/iotedge/commit/cf2eba9518a947bb09ebff5dcd6ae42f66d2d045) [23b40e1](https://github.com/Azure/iotedge/commit/23b40e1bf5e9cd04af1246b2214f0759c6446ea9)
* Preserve any extra properties in createOptions set by the user (NVidia support through moby) [5a6d506](https://github.com/Azure/iotedge/commit/5a6d5067c832ad85b39cc15d323442a973b3d3f7)  [a747950](https://github.com/Azure/iotedge/commit/a747950864403ee8f201bbf9e78aaf0a2c067411)


### Bug Fixes
* Expose the MaxOpenFiles setting in RocksDb to the user [f733205](https://github.com/Azure/iotedge/commit/f733205a9d88891398e3e7a65343575f97d106f9)
* Fix edgeHub stuck in a restart loop when `version` is specified [de9873e](https://github.com/Azure/iotedge/commit/de9873eeb61e207dbdbfb9158f085fa9b2f92d43)
* Remove potentially non-useful metrics from RQT [14b928b](https://github.com/Azure/iotedge/commit/14b928b4d46afdb28a8bf994eb698f3c07a2ee3e)
* Fix Api version set in "IOTEDGE_APIVERSION" to current Workload [e71286f](https://github.com/Azure/iotedge/commit/e71286f0351ee2d1dbef545ee03ba4fefee3b536)
* Returns an error message if logs file is too large for a request [9bea6e6](https://github.com/Azure/iotedge/commit/9bea6e6ffab8c4dd9a60e1040adda576c4d33719)
* Fix edge agent connectivity issue after receiving an exception [23ffb26](https://github.com/Azure/iotedge/commit/23ffb26484fd73f35e614c401a114a695b5339bf)
* Fix support bundle autofac [7c1706a](https://github.com/Azure/iotedge/commit/7c1706a19b1abc9256827c92f783d7f31bc6f806)
* Make edge agent reported state as "406" when modules are in backoff.  [5a68ced](https://github.com/Azure/iotedge/commit/5a68cedab1d0e97c89552dd51ed5657ebcf81dd8)
* Stop existing modules when iotedged starts [7066164](https://github.com/Azure/iotedge/commit/70661641799d16a940b191ee1c7faa24842fd4be)
* Reprovision device for all protocols when the connection status change reason is Bad_Credential. [3601a56](https://github.com/Azure/iotedge/commit/3601a566e728f697176398b7f92deb79b60278fe)
* Fix vulnerability issues for docker images. [d88fa52](https://github.com/Azure/iotedge/commit/d88fa52d910a71df0ea7b2d38b1e357514027f38) [7873079](https://github.com/Azure/iotedge/commit/7873079c5a3d4e28dcf6c979a1533d6d950fc428)


## Edge Hub
### Features
* Enable `MetricsHistogramMaxAge` [c550463](https://github.com/Azure/iotedge/commit/c550463919e0994ac2a977fe3cca66e02959d8ce) [c958739](https://github.com/Azure/iotedge/commit/c958739a18a1a9a1213ce8c22e3fa7823fba2dfd)
* Enable twin encrypt by default [12b7306](https://github.com/Azure/iotedge/commit/12b7306fe9b750a9014e064e2cbdbd777d36edd1)
* Support Plug-and-Play [f8da2f6](https://github.com/Azure/iotedge/commit/f8da2f65a98568c463a3083a81018a6a05ef7da7)
* Update SDK version {Microsoft.Azure.Devices.Client.1.28.0}. [cdf36b0](https://github.com/Azure/iotedge/commit/cdf36b01a7a18b0a4f9a1e3d5943fb9aa44029ea)
* Update codebase to dotnet 3.1. [f87a18a](https://github.com/Azure/iotedge/commit/f87a18a487ea0c05752254aaba04a4f89028120a)
* Install Trust Bundle. [4f85dcc](https://github.com/Azure/iotedge/commit/4f85dcc7d3fd6d4772d4a9b86ec3ecad651938fd)
* Add metrics upload to IoTHub feature . [eff5c85](https://github.com/Azure/iotedge/commit/eff5c859e101d14eda85b80513136a2f3a473892)
* Unify TLS protocol parsing. [f319228](https://github.com/Azure/iotedge/commit/f3192289d29be33222dedb93ce7d49ffc532fcd5)
* Add support for priorities on routes (limited to 0-9). [9cf0203](https://github.com/Azure/iotedge/commit/9cf02037ff0f43dc9f8a1e72cde09fd06507e2c8)
* Add support for Time-To-Live on routes. [2662d9c](https://github.com/Azure/iotedge/commit/2662d9cd46f0abbe91e1e0260b37d1f9372609a7)
* Add support module booting order in IoT Edge [6fce17b](https://github.com/Azure/iotedge/commit/6fce17bddff05b6d5f805b43330ec7a25a79ba2a)
* Add array support in twin [8a69b77](https://github.com/Azure/iotedge/commit/8a69b776c930f1697c4e6a173cc4a8dd4ee67b9c)

### Bug Fixes
* Fix incorrect source for Reported Property Updates (RPU) as telemetry messages [94e456c](https://github.com/Azure/iotedge/commit/94e456cfa3a3ff9135d032aaceaba12cf41bd803)
* Expose the MaxOpenFiles setting in RocksDb to the user [f733205](https://github.com/Azure/iotedge/commit/f733205a9d88891398e3e7a65343575f97d106f9)
* Correct `edgehub_messages_dropped_total` metric calculation [4233168](https://github.com/Azure/iotedge/commit/42331688ab9c8d60503ab9ad3f572efc812d016b)
* Make Histogram quantiles { 0.1, 0.5, 0.9 and 0.99 } [c550463](https://github.com/Azure/iotedge/commit/c550463919e0994ac2a977fe3cca66e02959d8ce) [64d488e](https://github.com/Azure/iotedge/commit/64d488ece9065dbfdd857ab47ba9b57054462da0)
* Automatically get cloud connection if adding device with subscriptions [063744d](https://github.com/Azure/iotedge/commit/063744d5920088f7c96eb4fde231979294fafb70)
* Fix processed message priority tagging for metrics [14aaee0](https://github.com/Azure/iotedge/commit/14aaee06c3140474e9607bbc59f1f45a23814dba)
* Fix Subscription Processing Workaround  [331aaf9](https://github.com/Azure/iotedge/commit/331aaf9965542852f33af05c9449b018c73094d4)
* Fix ECC certificates parsing [7411daf](https://github.com/Azure/iotedge/commit/7411dafd658d251f9f0565af5b7089bdfbe44a4b)
* Fix vulnerability issues for docker images. [d88fa52](https://github.com/Azure/iotedge/commit/d88fa52d910a71df0ea7b2d38b1e357514027f38) [7873079](https://github.com/Azure/iotedge/commit/7873079c5a3d4e28dcf6c979a1533d6d950fc428)


## iotedged
### Features
* Add the following metrics {provisioning type, virtualized environment} [e2ed141](https://github.com/Azure/iotedge/commit/e2ed141b569be5e4bd42c339b98ca7109ae4b940) [be747cc](https://github.com/Azure/iotedge/commit/be747cc0429f7c22df4c6d484347f62837aeb9b7)
* Enable DPS hub name check [7fe23f6](https://github.com/Azure/iotedge/commit/7fe23f6982b25dedd0b119e47db7cb971dae83bc)
* Enable iotedged support bundle [45e33a0](https://github.com/Azure/iotedge/commit/45e33a045d9c9f31abb3d9cccea0a8f9c10d39f5)
* Update Windows Moby engine and cli to latest release [3987b9e](https://github.com/Azure/iotedge/commit/3987b9ea181b8a8c03c3952d2978ce3857e37eb2)
* Update Rust to stable 1.42.0 [cf01536](https://github.com/Azure/iotedge/commit/cf01536ab40e2c4592ed9b2211394d9e9aa464b3)
* Support X.509 authentication type in external provisioning. [0a43fdb](https://github.com/Azure/iotedge/commit/0a43fdba90537d0747a3b51d2a8006a1f4a89d09)
* Add support for manual X.509 provisioning. [b872869](https://github.com/Azure/iotedge/commit/b872869169a76a2f334f0d2800847eaf5664c1b0)
* Better PVC story for iotedged Kubernetes. [debf498](https://github.com/Azure/iotedge/commit/debf4987b10adf49304ead523677d5f5507a3bf6)
* Update k8s-openapi to v0.7.1 [877c8e8](https://github.com/Azure/iotedge/commit/877c8e8f57e4926dafb8607340b24614c3f93984)
* Unify TLS protocol parsing. [f319228](https://github.com/Azure/iotedge/commit/f3192289d29be33222dedb93ce7d49ffc532fcd5)
* Add support to specify min TLS version in config.yaml [6b1e19b](https://github.com/Azure/iotedge/commit/6b1e19b5ef2c01a920e25129377bd57d5ef6e934)

### Bug Fixes
* Make `always_reprovision_on_startup` setting to DPS provisioning configurable [ab2de15](https://github.com/Azure/iotedge/commit/ab2de1510ae99ae8c83bc5d8144a8dc1d4287597)
* Fix IotEdgeSecurityDaemon.ps1 script for WSL2 [1766c1d](https://github.com/Azure/iotedge/commit/1766c1d97adbe2d3a1fd04e070e41209a6e90aec)
* Fix Edgelet unable to pull using certain passwords. [0569489](https://github.com/Azure/iotedge/commit/0569489084962cfd303e69ba578e41e4d70c95b2)
* Stop existing modules when iotedged starts [7066164](https://github.com/Azure/iotedge/commit/70661641799d16a940b191ee1c7faa24842fd4be)
* Update `iotedge check`'s Moby check for new Moby version scheme [1c30f57](https://github.com/Azure/iotedge/commit/1c30f57cc2cbf9407a2bd1323b835478accfed14)

# 1.0.10-rc2 (2020-08-26)
## Edge Agent
### Features
* Ability to remotely get support-bundle via edge agent direct method [b0a872a](https://github.com/Azure/iotedge/commit/b0a872aeca22865d2f1d558122aa62057d97669a) [186ff12](https://github.com/Azure/iotedge/commit/186ff12105bc360f21de6858fe750579475fe5f6)
* Edge agent periodically sends product quality telemetry. You can opt-out by setting the environment variable `SendRuntimeQualityTelemetry` to `false` for the edge agent. [f379462](https://github.com/Azure/iotedge/commit/f379462c1bb10caea0b17c15befcab0f410f1480)
* Edge agent now hash all instances of module ids in device telemetry. [46f40fc](https://github.com/Azure/iotedge/commit/46f40fcaccebcd5b9eb4fc006e30cd4db5ff22e4)
* Rename log upload method from `UploadLogs` to `UploadModuleLogs`  [b567801](https://github.com/Azure/iotedge/commit/b5678011925d0b8ee2eee716a9b7264863608e03)
* Rename reboot order from `priority` to be `startupOrder` [eed9c06](https://github.com/Azure/iotedge/commit/eed9c064ae0ad5342a2196bd42feff2d8b7e9cdc)
* Update SDK version {Microsoft.Azure.Devices.Client.1.28.0}. [cdf36b0](https://github.com/Azure/iotedge/commit/cdf36b01a7a18b0a4f9a1e3d5943fb9aa44029ea)

### Bug Fixes
* Fix edge agent connectivity issue after receiving an exception [23ffb26](https://github.com/Azure/iotedge/commit/23ffb26484fd73f35e614c401a114a695b5339bf)
* Fix support bundle autofac [7c1706a](https://github.com/Azure/iotedge/commit/7c1706a19b1abc9256827c92f783d7f31bc6f806)
* Make edge agent reported state as "406" when modules are in backoff.  [5a68ced](https://github.com/Azure/iotedge/commit/5a68cedab1d0e97c89552dd51ed5657ebcf81dd8)
* Stop existing modules when iotedged starts [7066164](https://github.com/Azure/iotedge/commit/70661641799d16a940b191ee1c7faa24842fd4be)

## Edge Hub
### Features
* Enable twin encrypt by default [12b7306](https://github.com/Azure/iotedge/commit/12b7306fe9b750a9014e064e2cbdbd777d36edd1)
* Support Plug-and-Play [f8da2f6](https://github.com/Azure/iotedge/commit/f8da2f65a98568c463a3083a81018a6a05ef7da7)
* Update SDK version {Microsoft.Azure.Devices.Client.1.28.0}. [cdf36b0](https://github.com/Azure/iotedge/commit/cdf36b01a7a18b0a4f9a1e3d5943fb9aa44029ea)

### Bug Fixes
* Automatically get cloud connection if adding device with subscriptions [063744d](https://github.com/Azure/iotedge/commit/063744d5920088f7c96eb4fde231979294fafb70)
* Fix processed message priority tagging for metrics [14aaee0](https://github.com/Azure/iotedge/commit/14aaee06c3140474e9607bbc59f1f45a23814dba)
* Fix Subscription Processing Workaround  [331aaf9](https://github.com/Azure/iotedge/commit/331aaf9965542852f33af05c9449b018c73094d4)
* Fix ECC certificates parsing [7411daf](https://github.com/Azure/iotedge/commit/7411dafd658d251f9f0565af5b7089bdfbe44a4b)

## iotedged
### Features
* Update Windows Moby engine and cli to latest release [3987b9e](https://github.com/Azure/iotedge/commit/3987b9ea181b8a8c03c3952d2978ce3857e37eb2)

### Bug Fixes
* Fix Edgelet unable to pull using certain passwords. [0569489](https://github.com/Azure/iotedge/commit/0569489084962cfd303e69ba578e41e4d70c95b2)
* Stop existing modules when iotedged starts [7066164](https://github.com/Azure/iotedge/commit/70661641799d16a940b191ee1c7faa24842fd4be)
* Update `iotedge check`'s Moby check for new Moby version scheme [1c30f57](https://github.com/Azure/iotedge/commit/1c30f57cc2cbf9407a2bd1323b835478accfed14)

# 1.0.10-rc1 (2020-06-26)
## Edge Agent
### Features
* Update codebase to dotnet 3.1. [f87a18a](https://github.com/Azure/iotedge/commit/f87a18a487ea0c05752254aaba04a4f89028120a)
* Install Trust Bundle. [4f85dcc](https://github.com/Azure/iotedge/commit/4f85dcc7d3fd6d4772d4a9b86ec3ecad651938fd)
* Add metrics upload to IoTHub feature [eff5c85](https://github.com/Azure/iotedge/commit/eff5c859e101d14eda85b80513136a2f3a473892)
* Add "Cmd", "Entrypoint", and "WorkingDir" translations for Kubernetes. [7cbc607](https://github.com/Azure/iotedge/commit/7cbc607ccc483d6e0ab9642be76c8b2d8bc09605)
* Add Experimental k8s create option feature for pod security context, resources, volumes, nodeSelector, and strategy. [cf2eba9](https://github.com/Azure/iotedge/commit/cf2eba9518a947bb09ebff5dcd6ae42f66d2d045) [23b40e1](https://github.com/Azure/iotedge/commit/23b40e1bf5e9cd04af1246b2214f0759c6446ea9)
* Update SDK version {Microsoft.Azure.Devices.1.21.0, Microsoft.Azure.Devices.Client.1.26.0}. [5148ee7](https://github.com/Azure/iotedge/commit/5148ee70b793963f74862e047039a412a488ab73)
* Preserve any extra properties in createOptions set by the user (NVidia support through moby) [5a6d506](https://github.com/Azure/iotedge/commit/5a6d5067c832ad85b39cc15d323442a973b3d3f7)  [a747950](https://github.com/Azure/iotedge/commit/a747950864403ee8f201bbf9e78aaf0a2c067411)

### Bug Fixes
* Reprovision device for all protocols when the connection status change reason is Bad_Credential. [3601a56](https://github.com/Azure/iotedge/commit/3601a566e728f697176398b7f92deb79b60278fe)
* Fix vulnerability issues for docker images. [d88fa52](https://github.com/Azure/iotedge/commit/d88fa52d910a71df0ea7b2d38b1e357514027f38) [7873079](https://github.com/Azure/iotedge/commit/7873079c5a3d4e28dcf6c979a1533d6d950fc428)

## Edge Hub
### Features
* Update codebase to dotnet 3.1. [f87a18a](https://github.com/Azure/iotedge/commit/f87a18a487ea0c05752254aaba04a4f89028120a)
* Install Trust Bundle. [4f85dcc](https://github.com/Azure/iotedge/commit/4f85dcc7d3fd6d4772d4a9b86ec3ecad651938fd)
* Add metrics upload to IoTHub feature . [eff5c85](https://github.com/Azure/iotedge/commit/eff5c859e101d14eda85b80513136a2f3a473892)
* Unify TLS protocol parsing. [f319228](https://github.com/Azure/iotedge/commit/f3192289d29be33222dedb93ce7d49ffc532fcd5)
* Add support for priorities on routes (limited to 0-9). [9cf0203](https://github.com/Azure/iotedge/commit/9cf02037ff0f43dc9f8a1e72cde09fd06507e2c8)
* Add support for Time-To-Live on routes. [2662d9c](https://github.com/Azure/iotedge/commit/2662d9cd46f0abbe91e1e0260b37d1f9372609a7)
* Add support module booting order in IoT Edge [6fce17b](https://github.com/Azure/iotedge/commit/6fce17bddff05b6d5f805b43330ec7a25a79ba2a)
* Update SDK version {Microsoft.Azure.Devices.1.21.0, Microsoft.Azure.Devices.Client.1.26.0}. [5148ee7](https://github.com/Azure/iotedge/commit/5148ee70b793963f74862e047039a412a488ab73)
* Add array support in twin [8a69b77](https://github.com/Azure/iotedge/commit/8a69b776c930f1697c4e6a173cc4a8dd4ee67b9c)

### Bug Fixes
* Fix vulnerability issues for docker images. [d88fa52](https://github.com/Azure/iotedge/commit/d88fa52d910a71df0ea7b2d38b1e357514027f38) [7873079](https://github.com/Azure/iotedge/commit/7873079c5a3d4e28dcf6c979a1533d6d950fc428)

## iotedged
### Features
* Update Rust to stable 1.42.0 [cf01536](https://github.com/Azure/iotedge/commit/cf01536ab40e2c4592ed9b2211394d9e9aa464b3)
* Support X.509 authentication type in external provisioning. [0a43fdb](https://github.com/Azure/iotedge/commit/0a43fdba90537d0747a3b51d2a8006a1f4a89d09)
* Add support for manual X.509 provisioning. [b872869](https://github.com/Azure/iotedge/commit/b872869169a76a2f334f0d2800847eaf5664c1b0)
* Better PVC story for iotedged Kubernetes. [debf498](https://github.com/Azure/iotedge/commit/debf4987b10adf49304ead523677d5f5507a3bf6)
* Update k8s-openapi to v0.7.1 [877c8e8](https://github.com/Azure/iotedge/commit/877c8e8f57e4926dafb8607340b24614c3f93984)
* Unify TLS protocol parsing. [f319228](https://github.com/Azure/iotedge/commit/f3192289d29be33222dedb93ce7d49ffc532fcd5)
* Add support to specify min TLS version in config.yaml [6b1e19b](https://github.com/Azure/iotedge/commit/6b1e19b5ef2c01a920e25129377bd57d5ef6e934)

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
