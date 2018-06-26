IoT Edge Build and Test Infrastructure
============================================

Build
-----

We use VSTS to build and release.

More information about VSTS is available [here](https://docs.microsoft.com/en-us/vsts/build-release/overview).

Our builds fall into these categories:
| Type                                      | Goal                                                                                                          | Trigger                                        | Target                      |
|-------------------------------------------|---------------------------------------------------------------------------------------------------------------|------------------------------------------------|-----------------------------|
| Checkin                                   | Ensures that the master branch builds clean and passes basic tests.                                           | When commits are pushed to a Pull Request (PR) | Linux - x64                 |
| Security Development Lifecycle (SDL)      | Scans and analyzes the code to detect security issues.                                                        | When commits are pushed to a PR                | Windows - x64               |
| Continuous Integration (CI)               | Runs end-to-end tests, builds Docker images & publishes to ACR, builds iotedgectl pypi archive.               | After a PR is merged to master                 | Linux - x64, Windows - x64  |
| Continuous Deployment (CD)                | Consumes artifacts from CI, deploys IoT Edge to a Linux VM and a Raspberry Pi and tests basic functionality.  | After a successful CI build                    | Linux - x64                 |
| Images                                    | Builds Docker images & publishes to ACR.                                                                      | Manual                                         | Linux - x64, Windows - x64  |
| Service Deployment Verification           | Runs smoke tests. Used to validate IoT Edge scenarios before the service deploys updates.                     | Manual                                         | Linux - x64                 |
| Release Build                             | Under the Release/ folder. Builds IoT Edge core runtime (C#) and iotedgectl tool (Python).                    | Manual                                         | Linux - x64, Windows - x64  |
| Release Publish                           | Under the Release/ folder. Builds Docker images from signed IoT Edge runtime binaries.                        | Manual                                         | Linux - x64, Windows - x64  |


Test
----

Our C# tests use [xUnit](https://xunit.github.io/docs/getting-started-dotnet-core). Each test is tagged with one of the following attributes:

| Attribute   | Goal                                                                                | Notes                                                                                         | Runs in build(s)  |
|-------------|-------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------|-------------------|
| Unit        | Fast, reliable, repeatable tests against low-level components of the codebase.      | Should not depend on external resources (e.g., network, disk) or configuration.               | Checkin           |
| Integration | Pretty fast tests that verify our interaction with dependencies _in isolation_.     | Require little/no setup an no external configuration.                                         | Checkin           |
| E2E         | High-level tests that verify core end-to-end scenarios.                             | Not suitable for scenarios which could be covered by unit or integration tests.               | CI                |
| Stress      | Specialized end-to-end tests for scenarios under heavy load.                        |                                                                                               | N/A               |
