# 1.0.6 (2022-03-15)

## Bug Fixes
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