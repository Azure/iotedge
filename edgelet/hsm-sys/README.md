# HSM-SYS crate

This crate is the unsafe C to Rust interface for the HSM API library.

This crate represents the functions that the HSM API implements. This crate is 
used by the HSM-RS crate to provide more Rust-friendly interfaces.

## TPM functionality

You may need additional setup for a TPM device see [README-TPM](README-TPM.md) for details.

## Memory allocation

The current HSPM API functions expect the calling function to allocate 
memory for the the caller to use.  The caller (in this case, the Rust crate) is 
expected to free this memory. 

## Maintenance.

This file was initially generated via the 
[bindgen](https://rust-lang-nursery.github.io/rust-bindgen/) tool from 
source [`hsm_client_data.h`](https://github.com/Azure/azure-iot-hsm-c/inc/hsm_client_data.h) 
and then hand-edited for conciseness and usability. We expect that the size of 
the API will remain fairly small and that continued editing by hand will be a 
small effort. The version of the HSM API this is based from is the "Commit Id" 
in `lib.rs`- please update the commit id when updating from a new version of 
the header file.

## Build dependencies

This crate is dependent on CMake being installed. On Debian based linux systems, 
this can be installed with 

```
sudo apt-get install build-essential cmake libcurl4-openssl-dev uuid-dev valgrind
```

On Windows:
1) Install CMake from [cmake.org](https://cmake.org/).
2) Install OpenSSL
    Open an admin level PS
    PS> cd edgelet\build\windows
    PS> . .\openssl.ps1
    PS> Get-OpenSSL
    Close PS


### Valgrind

Valgrind was added to the linux build dependencies. We are using Valgrind for detecting 
memory leaks, unassigned variables, and overruns in the dev mode iothsm library.

Valgrind slows down the tests considerably, so the iothsm library in hsm-sys runs tests with 
valgrind turned off by default.  If you wish to run valgrind, set the 
environment variable "RUN_VALGRIND".

## Linking

This crate needs to be linked to a library which implements the functions in 
`hsm_client_data.h`
