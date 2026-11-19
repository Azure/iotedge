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
| Service Deployment Verification      | Runs smoke tests. Used to validate IoT Edge scenarios before IoT Hub deploys updates.                    | Nightly or manual                              | Linux (amd64)                   |
| Build Release                        | Builds IoT Edge packages and Docker images for final release                                             | Manual                                         | Linux (amd64, aarch64, arm32v7) |
| Publish Release                      | Publishes IoT Edge packages and Docker images to public release locations.                               | Manual                                         | Linux (amd64, aarch64, arm32v7) |


Release branch pipeline maintenance
-----------------------------------

`main` builds IoT Edge 1.7.x; `release/1.6` services 1.6.x LTS.
Keep `release/1.5` scheduled until its end of support on November 10, 2026.

All times below are UTC. The weekly test schedules use different days to
reduce contention for shared test agents.

| Pipeline | main (1.7) | release/1.6 | release/1.5 (temporary) |
|----------|------------|-------------|------------------------|
| [CI build](../builds/misc/ci-build.yaml) | Monday 15:00 | Monday 15:00 | Monday 15:00 |
| [Connectivity](../builds/e2e/connectivity.yaml) | Sunday 00:00 | Monday 00:00 | Tuesday 00:00 |
| [Nested E2E](../builds/e2e/nested-e2e.yaml) | Wednesday 00:00 | Thursday 00:00 | Friday 00:00 |
| [ISA-95](../builds/e2e/isa-95-smoke-test.yaml) | Daily 00:00 | Daily 08:00 | Daily 16:00 |
| [Service deployment](../builds/service/service-deployment.yaml) | Not scheduled | Daily 04:00 | Not scheduled |
| [Base image update detection](../builds/release/detect-image-updates.yaml) | Daily 03:00 and 15:00 | Daily 03:00 and 15:00 | Daily 03:00 and 15:00 |

When adding or retiring a release branch:

- Update scheduled pipeline YAML **in each affected branch**. Adding a branch
  filter only on `main` does not register schedules for that release branch.
- Schedule service deployment verification only on the latest stable branch,
  currently `release/1.6` (1.6 LTS). Manual runs remain available on other branches.
- Keep each `ci-build` pipeline resource's `branch` set to the branch containing
  that YAML. This selects matching CI artifacts for scheduled and manual runs,
  including PR checks that reuse CI artifacts. Connectivity downloads on all
  architectures use the selected resource run rather than UI branch variables.
- CI and PR filters already include `main` and `release/*`.
  [E2E completion triggers](../builds/e2e/e2e.yaml) also include both.
  Azure DevOps evaluates completion filters from the pipeline's **Default branch
  for manual and scheduled builds** (verify it is `refs/heads/main`), then runs
  same-repository pipelines on the triggering branch and commit, using that
  CI run's artifacts regardless of the resource's default `branch`.
- Ensure pipeline UI schedules are absent: they override YAML schedules.
  After merging the changes into each branch, check Azure DevOps **Scheduled
  runs** for that branch and confirm release builds trigger E2E on the same branch.
- On November 10, remove the temporary `release/1.5` schedule entries from all
  three branches and disable its remaining automation as appropriate. Cron
  schedules do not expire automatically. The image-detection schedule and CI,
  PR, and completion filters still match all `release/*` branches.

See Azure DevOps documentation for [scheduled trigger branch evaluation](https://learn.microsoft.com/azure/devops/pipelines/process/scheduled-triggers#branch-considerations-for-scheduled-triggers)
and [pipeline completion branch evaluation](https://learn.microsoft.com/azure/devops/pipelines/process/pipeline-triggers#branch-considerations).

Test
----

Our C# tests use [xUnit](https://xunit.github.io/docs/getting-started-dotnet-core). Each test is tagged with one of the following attributes:

| Attribute   | Goal                                                                                            | Notes                                                                                 | Runs in build(s)  |
|-------------|-------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|-------------------|
| Unit        | Fast, reliable, repeatable tests against low-level components of the codebase.                  | Should not depend on external resources (e.g., network, disk) or configuration.       | Checkin           |
| Integration | Tests that verify interaction with dependencies _in isolation_ and core end-to-end scenarios.    | Require external configuration.                                                       | CI                |
| Stress      | Specialized end-to-end tests for scenarios under heavy load.                                    |                                                                                       | N/A               |

Besides these tests, E2E scenario testing is implemented in IotEdgeQuickstart project in smoke folder.  These tests will be run in Linux, Raspberry Pi and Windows to ensure all core IoT Edge features function correctly.
