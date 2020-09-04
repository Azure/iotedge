1. `azsecretd` depends on `aziot-keyd` for encrypting and decrypting
  data. Please refer to the [`iot-identity-service`
  documentation](../extern/iot-identity-service/docs/running/aziot-keyd.md)
  for information regarding how to configure `aziot-keyd`.

1. Define the configuration for `azsecretd` in
  `/etc/azsecret/config.toml`. Below is a basic template:

  ```
  [credentials] # AAD identity must have access to desired vaults
  tenant_id = "{AAD_TENANT_ID}"
  client_id = "{AAD_CLIENT_ID}"
  client_secret = "{AAD_CLIENT_SECRET}"

  [[principal]]
  name = "iotedged"
  uid = {IOTEDGE_UID}

  [local]
  key_service = "{KEY_SERVICE_URI}"

  [certificates]
  ```

1. Start `aziot-keyd`. In the absence of socket activation, one may
  need `sudo`:

  ```
  # from ../extern/iot-identity-service
  cargo build -p aziot-keyd &&  \
      sudo LD_LIBRARY_PATH=target/debug target/debug/aziot-keyd
  ```

1. Start `azsecretd`. Since socket activation is not ready at the
  moment, `sudo` will be necessary for opening the command socket:

  ```
  # from ../
  cargo build && sudo target/debug/azsecretd
  ```