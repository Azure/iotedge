This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

# Integrate your device HSM with Azure services and software
Manufacturers can implement the API defined by this repository to allow Azure software and services like IoT Edge and Device Provisioning Service to use their HSM for secure operations.

## Implementing the API
To make your HSM available to Azure software and services, you must create a library (static or shared, e.g., `libiothsm.a|.so` on Linux, `iothsm.lib|.dll` on Windows) which implements the functions declared in the [hsm_client_data.h](inc/hsm_client_data.h) header file. Functions are organized into three interfaces: **TPM**, **x509**, and **Crypto**. There are also a few functions you'll implement to manage each interface, e.g. for the TPM interface, `hsm_client_tpm_init()`, `hsm_client_tpm_interface()` and `hsm_client_tpm_deinit()`.

> The **TPM** and **x509** interfaces are intended to be mutually exclusive. You can tell CMake which one you did **not** implement when you [build the validation suite](#validation).

See the `samples/` folder in this repository for examples. See the `docs/` folder for API reference documentation.

## Validation

A suite of validation tests is available to help you determine whether your library will work with Azure IoT. After cloning this repository, use the following commands from the root directory to build and run the validation suite:

```
mkdir build
cd build
cmake -Dvalidate_hsm=ON -Dhsm_library=path/to/your/hsm/library ..
```
> Optionally add `-Dexclude_x509=ON` or `-Dexclude_tpm=ON` to the cmake command above to exclude an interface you didn't implement.

> The path you provide to the `hsm_library` argument must be a file that can be linked by the compiler, e.g., `.a` or `.so` on Linux, `.lib` (static or import library, NOT `.dll`) on Windows.

```
cmake --build .
tools/hsm_validator/hsm_validation_runner
# On Windows: tools\hsm_validator\Debug\hsm_validation_runner.exe
```

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.
