# Azure IoT Edge Release Process
A couple of VSTS builds and a release are used to publish the Edge Agent, Edge Hub, and Simulated Temperature Sensor container images.
These jobs are as follows:
* [Azure-IoT-Edge-Core Linux Release Build](https://msazure.visualstudio.com/One/_build/index?context=mine&path=%5CCustom%5CAzure%5CIoT%5CEdge%5CCore%5CRelease&definitionId=12790&_a=completed)
* [Azure-IoT-Edge-Core Windows RS3 Release Build](https://msazure.visualstudio.com/One/IoT-FieldGateway/_build/index?context=allDefinitions&path=%5CCustom%5CAzure%5CIoT%5CEdge%5CCore%5CRelease&definitionId=13577&_a=completed)
* [Azure-IoT-Edge Release](https://msazure.visualstudio.com/one/_release?definitionid=643&_a=releases)

The first two builds can be run in parallel.
They build the os and architecture specific docker containers and publish them to the release Azure Container Registry.

The release is used to create and publish a manifest for the multi-architecture release.

## Release Process
The following walks through the required steps to produce a release.

### Step 0 - Update the version 
Update the version in the following 2 files - 
- versionInfo.json(../versionInfo.json)
- setup.py (../edge-bootstrap/python/setup.py)

### Step 1 - Tag the git repo
We must tag each build in the git repository so that we can track what we are shipping.
Right now, tagging is a manual process, performed on a dev box and pushed to VSTS.

For pre-Public Preview, we are using a version number of the format: `1.0.0-previewXXX` (e.g. `1.0.0-preview001`).
When creating a new release, bump the preview number by one.
This document assumes a new version of `1.0.0-preview001` off of `master`.
The following commands are used to tag the repo:

```git
git tag 1.0.0-preview001
git push origin master --tags
```

This tags the current commit with `1.0.0-preview001` and pushes the tags to the VSTS repo.
The `--tags` is required to push the newly created tag.
Git does not do this by default.

### Step 2 - Build the branch
Two seperate VSTS builds are used for linux and windows - 
- Azure-IoT-Edge-Core Linux Release Build
- Azure-IoT-Edge-Core Windows RS3 Release Build

> Note: All IoT Edge release builds are under Build Definitions/Custom/Azure/IoT/Edge/Core/Release

For each build, follow these steps - 
Click `Queue new build...` in the upper right.

![Queue new build...][queue]

This brings up a dialog to select the tag and specify the version number.
Select `Branch` and then `Tags` to see all of the pushed tags.
Select the tag from Step 1.

![Select tag][queue-tag]

Update the version variable to the tag name.
(Unfortunately this can't be automated).
The Branch option specifies which commit to build and the version specifies the image tag for the resulting docker images.

![Select tag][queue2]

Complete this for both the Linux and Windows build jobs listed at the top.
These jobs don't currently run tests, so they can be run in parallel.

### Step 3 - Sign the bits
The Edge bits need to be signed before being published. We use a physical machine owned by the SDK team to sign our binaries. 
Here are the steps on how to do that - 
- Download the publish folders from the previous build branch step. 
- Log on to the lab machine azureiot-lab01. (You need to get the credentials for this machine separately)
- Copy only the edge .Net binaries to a folder called ToSign (it can be in any location, but they are generally put under c:\edge\)
- Create another folder for the signed binaries (say "signed")
- Open an elevated prompt and run the following command - 
csu.exe /s=True /w=True /i=InputFolderPath /o=OutputFolderPath /c1=401 /d="Signing Azure IoT Managed Client binaries – Edge release"
- After the job is complete, the output folder should contain the signed binaries. Copy them back to the publish folder, zip the folder and upload it to the Azure Storage account azureiotedgecodesign, in the container codesign.

### Step 4 - Build docker images
Kick off the following 2 builds for building Linux and Windows docker images - 
- Azure-IoT-Edge-Core Linux Release Publish
- Azure-IoT-Edge-Core Windows RS3 Release Publish

![Build docker images][dockerimages]

The signed.published parameter is the URL to the blob from step #3 (the url should contain a SAS token key)

### Step 5 - Build manifest images
Kick off the VSTS job to build the manifest images - 
-   Azure-IoT-Edge-Core Build Manifest Images

### Step 5 - Release
A "release" is used to create the manifest images.
It takes the builds from the previous step as input, and uses the manifest-tool to create manifest images and push to the Azure Container Registry.
The release is located [here](https://msazure.visualstudio.com/One/_release?definitionId=643&_a=releases).
Click the `+ Release` button on the top left, under Azure-IoT-Edge Release.

![Release][release]

This brings up a dialog picker to select the build artifacts from the previous steps.

![Release dialog][release2]

Select the previous build numbers.
The `repo` selection can be set to `master`.
It's only required for some scripts that are not included in the build output.
Select create.

![Release dialog filled out][release3]

If all goes well, you should have the following images created in the registry:
* `azureiotedge/edge-agent:<version>`
* `azureiotedge/edge-hub:<version>`
* `azureiotedge/simulated-temperature-sensor:<version>`

## Configuration
The previous jobs use variables to configure the location of the registry and the credentials.
If they need to be change, they must be updated for each job individually.
They are changed by editing the job and clicking the `Variables` tab.
Here is an example from the linux build job:

![Configure the build variables][config]

[queue]: images/release-queue.png
[queue2]: images/release-queue2.png
[queue-tag]: images/release-queue-tag.png
[release]: images/release-release.png
[release2]: images/release-release2.png
[release3]: images/release-release3.png
[config]: images/release-config.png
[dockerimages]:images/release-dockerimages.png
