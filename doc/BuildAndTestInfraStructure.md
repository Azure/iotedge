Azure IoT Edge Build and Test Infrastructure
============================================

The goal of this document is to describe our build infrastructure (based on VSTS) and also our test infrastructure (which tests runs on which build definition).

Build Infrastructure
--------------------

Our builds (Continuous Integration (CI), Nightly Builds and Release builds) run today on VSTS infrastructure. 
It's located [here](https://msazure.visualstudio.com/One/IoT-Platform-Edge/_Build/index?_a=allDefinitions&path=%5CCustom%5CAzure%5CIoT%5CEdge%5CCore%5C).

To learn more about VSTS Build and Release Infrastructure go [here](https://docs.microsoft.com/en-us/vsts/build-release/overview).

Below you can find a list of type of builds we plan to have: 
| Type                                       | Goal                                                                                                                                                    | Trigger                         | Target OS/Platform                         |
|--------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------|---------------------------------------|
| CI - Continuous Integration                | These build definition should be named ending with CI and the goal will be to make sure we don't break our builds and our Basic Tests.                  | Before every check in (Check in Gate) | Linux - x64  |
| Security and Credential scans              | Runs our Security and credential scans.                                                                                                                 | Before every check in (Check in Gate) | Windows - x64 |
| Nightly                                    | The goal of the nightly build is to start a full process of check, including triggering images build, running end 2 end, Stress and Integration tests.  | Nightly at 3 am                       | Linux - x64, Windows - x64 and macOS-x64 |
| Images                                     | Definition responsible to create full images (currently docker)                                                                                         | After Nightly build succeed.          | Linux - x64, Windows - x64 and macOS-x64 |
| Deployment Verification                    | Definition to be used by the service team to make sure they don't break any Azure IoT Edge Scenario and basic End2End Test when they make a service change. | Manually by Request               | Linux - x64|
| Release                                    | Set of definitions used to release our product. E.g: Signing binaries, publishing packages, etc.                                                        | Manually at release.                  | Linux - x64, Windows - x64 and macOS-x64 |


Test Infrastructure
-------------------

Today our tests are built based also on Visual Studio and VSTS Build System. 
We are also using the current test framework [XUnit](https://xunit.github.io/docs/getting-started-dotnet-core).

Here are a list of tags we are using today and which build definition they run:

| Test Tag    | Goal                                                                                                                   | Notes                                                                                                                                                                                                                  | Build Definition it runs                                    |
|-------------|------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------|
| Unit        | The goal is that these tests are fast, reliable, repeatable, and as close as possible to the code under test.          | Ideally these tests should run without any dependency or extra configuration. Mock can be use as a technique to achieve this goal.                                                                                     | Azure-IoT-Edge-Core CI                                      |
| Integration | Test the interaction with one dependency at a time ("one" rather than "some").                                         | These tests should also be fast and these can Run on a Dev Machine without any infrastructure other than XUnit.                                                                                                        | Azure-IoT-Edge-Core-Nightly                                 |
| End 2 End   | Test our product End2End, but not just basic positive scenarios, but also contain some Negative tests, Bug Fixes, etc. | Tests on these category should also run fast, otherwise move the test to LongHaul or Stress.                                                                                                                           | Azure-IoT-Edge-Core SDT ??                                  |
| Stress      | Tests our performance under heavy usage                                                                                | Requires the same infra as BVT and End2End, but shall expect to run for a long period of time.                                                                                                                         | Create a new one, since it can run for long period of time? |
| Long Haul   | Test our product during a long period of time                                                                          | Requires full Deployment, but run into a more realistic load than the Stress test during a long period of time to test the Endurance of our product.                                                                   | Create a new one, since it can run for long period of time? |