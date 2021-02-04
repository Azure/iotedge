This directory contains test files for the `iotedge init import` tests.

For each test, `input.yaml` is the old iotedged config, and when given to to `iotedge init import` should produce the five services' configs in `keyd.toml`, `certd.toml`, `identityd.toml`, `tpmd.toml` and `edged.yaml`, plus the aziot-edged principal in `edged-principal.toml`. In the tests that involve a symmetric key, the `device-id` file stores the contents of the `/var/secrets/aziot/keyd/device-id` file that holds the symmetric key and is preloaded into keyd.
