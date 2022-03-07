IoT Edge Build and Test Infrastructure
============================================

Build
-----

We use [Azure DevOps](https://docs.microsoft.com/en-us/azure/devops/pipelines/overview) to build and release.

Our builds fall into these categories:

| Type                                 | Goal                                                                                                     | Trigger                                        | Target                          |
|--------------------------------------|----------------------------------------------------------------------------------------------------------|------------------------------------------------|---------------------------------|
| Checkin                              | Ensures that a branch builds clean and passes basic tests.                                               | When commits are pushed to a Pull Request (PR) | Linux (amd64)                   |
| Security Development Lifecycle (SDL) | Scans and analyzes the code to detect security issues.                                                   | When commits are pushed to a PR                | Windows (amd64)                 |
| Continuous Integration (CI)          | Ensures that a branch builds clean and passes integration tests, builds host packages and Docker images. | After a PR is merged                           | Linux (amd64, aarch64, arm32v7) |
| Continuous Deployment (CD)           | Consumes artifacts from CI, deploys IoT Edge to supported devices and tests basic functionality.         | After a successful CI build                    | Linux (amd64, aarch64, arm32v7) |
| Service Deployment Verification      | Runs smoke tests. Used to validate IoT Edge scenarios before IoT Hub deploys updates.                    | Manual                                         | Linux (amd64)                   |
| Build Release                        | Builds IoT Edge packages and Docker images for final release                                             | Manual                                         | Linux (amd64, aarch64, arm32v7) |
| Publish Release                      | Publishes IoT Edge packages and Docker images to public release locations.                               | Manual                                         | Linux (amd64, aarch64, arm32v7) |


Test
----

Our C# tests use [xUnit](https://xunit.github.io/docs/getting-started-dotnet-core). Each test is tagged with one of the following attributes:

| Attribute   | Goal                                                                                            | Notes                                                                                 | Runs in build(s)  |
|-------------|-------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|-------------------|
| Unit        | Fast, reliable, repeatable tests against low-level components of the codebase.                  | Should not depend on external resources (e.g., network, disk) or configuration.       | Checkin           |
| Integration | Tests that verify interaction with dependencies _in isolation_ and core end-to-end scenarios.    | Require external configuration.                                                       | CI                |
| Stress      | Specialized end-to-end tests for scenarios under heavy load.                                    |                                                                                       | N/A               |

Besides these tests, E2E scenario testing is implemented in IotEdgeQuickstart and LeafDevice project in smoke folder.  These tests will be run in Linux, Raspberry Pi and Windows to ensure all core IoT Edge features function correctly.
