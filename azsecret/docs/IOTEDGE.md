Custom builds of `iotedged` and the IoT Edge Agent are necessary for
exposing `azsecret` functionality to edge modules. In this document,
we will explain how to create these builds. For the time being, the
code containing the necessary changes is only available in
https://github.com/R25l84IHeXjIxy6HO1QXn0y0Dq9mt8EN/iotedge. Once
[this pull request](https://github.com/Azure/iotedge/pull/3483) is
merged, the code can also be obtained from the `feature/secretstore`
branch of https://github.com/Azure/iotedge. Please consult these two
items from the `iotedge` documentation for information on how to set
up your build environment:
- https://github.com/Azure/iotedge/blob/master/doc/devguide.md
- https://github.com/Azure/iotedge/blob/master/edgelet/doc/devguide.md
This guide assumes the presence of a working `iotedge` configuration
file, referred to here as `$IOTEDGE_CONFIG`. It also assumes that you
have a usable container registry for storing the modified edge agent
container.

1. Clone the modified `iotedge` repository:

  ```
  git clone --recurse-submodules https://github.com/Azure/iotedge.git
  cd iotedge
  ```

1. Build `iotedged`:

  ```
  # from iotedge repository root
  cd edgelet
  cargo build -p iotedged
  ```

1. Modify `$IOTEDGE_CONFIG` to include the following:

  ```
  secret:
    secret_host: "unix:///var/run/azsecret.sock"
  ```

  - WARNING: if your operating system uses `systemd`, the `iotedge`
    daemon was likely configured to use socket activation. This can
    be determined by inspecting the `.listen` field of
    `$IOTEDGE_CONFIG` for whether it matches the following:

    ```
    listen:
      management_uri: "fd://..."
      workload_uri: "fd://..."
    ```

    If it does, then you will either need to change your
    `iotedge.service` unit file to point to the `iotedged` build
    artifact generated above, or you can change the configuration to
    not use socket activation. Copying the values from `.connect` to
    `.listen` is the simplest and safest workaround.

1. Build the edge agent image:

  ```
  # from iotedge repository root
  cd edge-agent/src/Microsoft.Azure.Devices.Edge.Agent.Service
  dotnet publish -f netcoreapp3.1 -o publish
  docker build --no-cache -t YOUR_CONTAINER_REGISTRY/edgeAgent -f ./publish/docker/HOST_OS/HOST_ARCH/Dockerfile ./publish/
  docker push YOUR_CONTAINER_REGISTRY/edgeAgent
  ```

1. In future deployments, replace the default edge agent image with
  the custom image.
