# 1.1.2 (2023-02-14)

The following Docker images were updated because their base images changed:
* azureiotedge-metrics-collector

# 1.1.1 (2023-01-18)

The following Docker images were updated because their base images changed:
* azureiotedge-metrics-collector

# 1.1.0 (2023-01-05)

## What's new in 1.1?

The Metrics Collector 1.1 module is a refresh to align with the latest (1.4) version of IoT Edge. Its functionality remains unchanged. Users can confidently update Metrics Collector 1.0 in their IoT Edge deployments to the new 1.1 version. Changes include (see [71584c0](https://github.com/Azure/iotedge/commit/71584c0270b5456e20e27d6af447496c5a215d29)):
* Update from .NET Core 3.1 to .NET 6.0
* Move arm32v7/arm64v8 Docker images from Ubuntu to Alpine to be consistent with the amd64 image and to reduce image size
* Upgrade the IoT device SDK to a version that is consistent with the latest (1.4) version of IoT Edge

## Upgrade notes

Starting with the Metrics Collector 1.1 release, Windows Docker images are no longer provided.

## Bug Fixes
* Update Newtonsoft.Json dependency to 13.0.2 ( [4dca27b](https://github.com/Azure/iotedge/commit/4dca27be61f40b7b7944ecb4a38e3cd5f8f2867f) )

# 1.0.12 (2022-12-13)

The following Docker images were updated because their base images changed:
* azureiotedge-metrics-collector

# 1.0.11 (2022-11-08)
### Bug Fixes
* Replace instances of Console.WriteLine with standard logging pattern [e33191b](https://github.com/Azure/iotedge/commit/e33191b775523bed32e30a34cc8f4bbb257dfe02)

# 1.0.10 (2022-09-13)

### Bug Fixes
* Fix bug where failing connection to Edge Hub blocks independent AzureMonitor upload path [14c78fe](https://github.com/Azure/iotedge/commit/14c78fea9de250ef54ef6129e36f55587726dd46)
* Update SharpZipLib and Newtonsoft.Json to patch security vulnerability [1b483e4](https://github.com/Azure/iotedge/commit/1b483e4f114593b9cb40be65598c83bea6811444)
* Update base image to include .NET security fixes from [.NET Core 3.1.28 - August 9, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.28/3.1.28.md)

# 1.0.9 (2022-07-13)

### Bug Fixes
* Update base image to include .NET reliability and non-security fixes from [.NET Core 3.1.27 - July 12, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.27/3.1.27.md)
* Update base image to include .NET security fixes from [.NET Core 3.1.26 - June 14, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.26/3.1.26.md)
* Fix configuration key for the 'AddIdentityTags' experimental feature flag [d89dfe5](https://github.com/Azure/iotedge/commit/d89dfe5815905942bea500888ff361d2ed308313)

# 1.0.8 (2022-05-24)

### Bug Fixes
- Update Base Images to address Microsoft .NET Security Updates for [CVE-2022-23267](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-23267), [CVE-2022-29117](https://github.com/dotnet/announcements/issues/220), [CVE-2022-29145](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2022-29145), OpenSSL vulnerability [USN-5402-1](https://ubuntu.com/security/notices/USN-5402-1), curl vulnerability [USN-5412-1](https://ubuntu.com/security/notices/USN-5412-1), and OpenLDAP Vulnerability [USN-5424-1](https://ubuntu.com/security/notices/USN-5424-1)
- Experimental feature to [support scraping of 3rd party containers that provide prometheus metrics without iot edge specific tags](https://feedback.azure.com/d365community/idea/c5cdb9ba-398a-ec11-a81b-0022484bfd94). [Special thanks to user "alaendle" for reporting the issue, creating the user voice feedback, and contributing a potential solution.]

# 1.0.7 (2022-04-27)

### Bug Fixes
- Update Base Images to address gzip vulnerability [CVE-2022-1271](https://ubuntu.com/security/CVE-2022-1271)

# 1.0.6 (2022-03-15)

### Bug Fixes
- Update Base Images for a Security Patch [.NET Core 3.1.23 - March 8, 2022](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.23/3.1.23.md)

# 1.0.5 (2022-03-08)

### Bug Fixes
- Update references to default branch [7e50406](https://github.com/Azure/iotedge/commit/7e504060261634582434c394639b738f620015e3)
- Dotnet runtime security patch [patch](https://hub.docker.com/_/microsoft-dotnet-runtime)

# 1.0.4 (2022-02-11)

### Bug Fixes
- Use stable tags in our Windows Docker images [0ac3602](https://github.com/Azure/iotedge/commit/0ac3602f2fa5dd08b20826f68394a59f9434a691)

# 1.0.3 (2022-01-28)

### Bug Fixes
- Simplify the process of building arm images by removing intermediate steps [5a793bf](https://github.com/Azure/iotedge/commit/5a793bf02bc2701efb101da3a578f5583f34ccce)
- Rebuild Docker images to get security updates in base images (specifically, the [December .NET Core 3.1 update](https://github.com/dotnet/core/blob/main/release-notes/3.1/3.1.22/3.1.22.md?WT.mc_id=dotnet-35129-website)) [7b689b3](https://github.com/Azure/iotedge/commit/7b689b3d58f2b732f7ff5138ba36c9c331449322)
  - _Note_: Only Linux images were updated for this release. Windows images were overlooked, but will be updated in the next release.

# 1.0.2 (2021-10-22)

### Bug Fixes

- Bug Fix: Filtering metrics does not work if endpoint is specified [p6dab495](https://github.com/Azure/iotedge/commit/6dab4953de62ecfb1c410c9ed9faf16ce5470212)
- Update Base Images for Security Patch. [75bb5ea](https://github.com/Azure/iotedge/commit/75bb5eabb8c243d56ad3477b79b429060c7cb2d3)
- Bumped versions of Microsoft.Extensions.* dependencies [842c8c3](https://github.com/Azure/iotedge/commit/842c8c3d56e7ebc77aefd25a40bc38ffa96ba118)

# 1.0.1 (2021-07-13)

### Bug Fixes

- Add configuration for iothub reconnect frequency and azure domain. Fix edge case with hub resource id log filter. [057da6a](https://github.com/Azure/iotedge/commit/057da6a80bd844c99d64ea56afd433a3a9daf7f6)

# 1.0.0 (2021-06-03)

Initial release of the Metrics Collector module
