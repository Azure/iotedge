# IoT Edge Security Daemon - Dev Guide

There are two options for building the IoT Edge Security Daemon.

1. Build the OS packages. If you just want to build something that you can install on your device, and are not making frequent changes to the daemon's source code, you probably want this option.

1. Build the daemon binaries. If you want to make frequent changes to the daemon's source code and run it without installing it, you probably want this option.


## Building OS packages

### Linux

Linux packages are built using the `edgelet/build/linux/package.sh` script. Set the following environment variables, then invoke the script:

1. `PACKAGE_OS`: This is the OS on which the resulting packages will be installed. It should be one of `centos7`, `debian9`, `debian10`, `debian11`, `ubuntu18.04`, or `ubuntu20.04`

1. `PACKAGE_ARCH`: This is the architecture of the OS on which the resulting packages will be installed. It should be one of `amd64`, `arm32v7` or `aarch64`.

For example:

```sh
git clone --recurse-submodules 'https://github.com/Azure/iotedge'
cd iotedge/

PACKAGE_OS='debian10' PACKAGE_ARCH='arm32v7' ./edgelet/build/linux/package.sh
```

The packages are built inside a Docker container, so no build dependencies are installed on the device running the script. However the user running the script does need to have permissions to invoke the `docker` command.

Note that the script must be run on an `amd64` device. The `PACKAGE_ARCH=arm32v7` and `PACKAGE_ARCH=aarch64` builds are done using a cross-compiler.

Once the packages are built, they will be found somewhere under the `edgelet/target/` directory. (The exact path under that directory depends on the combination of `PACKAGE_OS` and `PACKAGE_ARCH`. See `builds/misc/packages.yaml` and `builds/misc/packages.slow.yaml` for the exact paths.)

If you want to run another build for a different combination of `PACKAGE_OS` and `PACKAGE_ARCH`, make sure to clean the repository first with `sudo git clean -xffd` so that artifacts from the previous build don't get reused for the next one.

Note: For the following targets, `qemu-user-static` must be installed on the host and registered with `binfmt`:

- `PACKAGE_OS=centos7 PACKAGE_ARCH=arm32v7`
- `PACKAGE_OS=centos7 PACKAGE_ARCH=aarch64`

If that has not been done, `package.sh` prints an error message explaining how to do that.

This is because these targets do not have functional cross-compilers, so their builds are done as native builds emulated using qemu. Be aware that these builds are much slower - where a native build might take 15m, a qemu build might take 2h30m.


### Windows

See "Building daemon binaries" below.


## Building daemon binaries

### Dependencies

The daemon is written in [Rust,](https://www.rust-lang.org/) so you will need an installation of the Rust compiler. It is recommended to use [`rustup`](https://www.rustup.rs/) to install a Rust toolchain. For Linux, some distributions have their own packages for `rustc` and `cargo`, but these might not match the toolchain used by our code and may fail to build it.

After installing `rustup`, or if you already have it installed, install the toolchain that will be used to build the daemon binaries.

```sh
rustup self update   # Update rustup itself to the latest version

git clone --recurse-submodules 'https://github.com/Azure/iotedge'
cd iotedge/edgelet/

rustup update   # Install / update the toolchain used to build the daemon binaries.
                # This is controlled by the rust-toolchain file in this directory.
                # For the main branch, this is the latest "stable" toolchain.
                # For release branches, this is a pinned Rust release.
```

In addition, building the daemon binaries also requires these dependencies to be installed:

#### CentOS 7

```sh
yum update
yum install \
    cmake curl git make rpm-build \
    gcc gcc-c++ \
    libcurl-devel libuuid-devel openssl-devel
```

#### Debian 9-11

```sh
apt-get update
apt-get install \
    binutils build-essential ca-certificates curl cmake debhelper file git make \
    gcc g++ pkg-config \
    libcurl4-openssl-dev libssl-dev uuid-dev
```
#### Ubuntu 18.04, 20.04

```sh
apt-get update
apt-get install \
    binutils build-essential ca-certificates curl cmake debhelper dh-systemd file git make \
    gcc g++ pkg-config \
    libcurl4-openssl-dev libssl-dev uuid-dev
```

#### macOS

1. Install the dependencies using [Homebrew](https://brew.sh/) package manager

    ```sh
    brew update
    brew install cmake openssl
    ```

1. Set the `OPENSSL_DIR` and `OPENSSL_ROOT_DIR` environment variables to point to the local openssl installation.

    ```sh
    export OPENSSL_DIR=/usr/local/opt/openssl
    export OPENSSL_ROOT_DIR=/usr/local/opt/openssl
    ```

#### Windows

1. Install Visual Studio 2017 / 2019, or the Build Tools for Visual Studio 2017 / 2019. Ensure the components for building C / C++ are installed.

1. Install `cmake` from <https://cmake.org/> or with [`choco`](https://chocolatey.org/) or [`scoop`.](https://scoop.sh/) Ensure `cmake` is in `PATH` after installation.

1. Install `vcpkg`

    ```powershell
    git clone https://github.com/Microsoft/vcpkg
    cd vcpkg
    .\bootstrap-vcpkg.bat
    ```

1. Install openssl

    ```powershell
    .\vcpkg install openssl:x64-windows
    ```

1. Set the `OPENSSL_DIR` and `OPENSSL_ROOT_DIR` environment variables to point to the local openssl installation.

    ```powershell
    # $PWD is the root of the vcpkg repository

    $env:OPENSSL_DIR = "$PWD\installed\x64-windows"
    $env:OPENSSL_ROOT_DIR = "$PWD\installed\x64-windows"
    ```


### Build

To build the project, use:

```sh
cd edgelet/

cargo build -p aziot-edged -p iotedge
```

This will create `aziot-edged` and `iotedge` binaries under `edgelet/target/debug`


### Run

In order to locally run aziot-edged, there is a dependency on running Azure IoT Identity Service.The following instruction can be used to run aziot-edged locally:
1. Clone the [identity service repo](https://github.com/Azure/iot-identity-service)
2. Build Binaries using [these build steps](https://github.com/Azure/iot-identity-service/blob/main/docs-dev/building.md)
3. Make directories and chown them to your user
    ```sh
    mkdir -p /run/aziot /var/lib/aziot/{keyd,certd,identityd,edged} /var/lib/iotedge /etc/aziot/{keyd,certd,identityd,tpmd,edged}/config.d
    
    chown -hR $USER /run/aziot /var/lib/aziot/ /var/lib/iotedge /etc/aziot/
    ```
4. Copy Provisioning File and Fill out the provisioning parameters. Example : For Provisioning via Symmetric Keys Use [these instructions](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-symmetric?view=iotedge-2020-11&tabs=azure-portal%2Cubuntu)

    ```sh
      cd <iot-edge-path>/edgelet
      cp contrib/config/linux/template.toml /etc/aziot/config.toml
    ```
5. Modify the Daemon configuration section in /etc/aziot/config.toml to match this
    ```toml
    [connect]
    workload_uri = "unix:///var/run/iotedge/workload.sock"
    management_uri = "unix:///var/run/iotedge/management.sock"


    [listen]
    workload_uri = "unix:///var/run/iotedge/workload.sock"
    management_uri = "unix:///var/run/iotedge/management.sock"
    min_tls_version = "tls1.0"
    ```
   This is because when running locally or without systemd, LISTEN_FDNAMES environment variable is not passed to aziot-edged and hence we explicitly need to specify the listen sockets.

6. Apply Config.
    ```sh
        cd <iot-edge-path>/edgelet
        cargo run -p iotedge -- config apply
    ```
7. Run keyd service in a separate shell
    ```sh
       cd <iot-identity-service-path>
       cargo run --target x86_64-unknown-linux-gnu -p aziotd -- aziot-keyd
    ```
8. Run Identityd service in a separate shell
     ```sh
       cd <iot-identity-service-path>
       cargo run --target x86_64-unknown-linux-gnu -p aziotd -- aziot-identityd
    ```
9. Run Certd Service in a separate shell
    ```sh
       cd <iot-identity-service-path>
       cargo run --target x86_64-unknown-linux-gnu -p aziotd -- aziot-certd
    ```
10. Finally, Run aziot-edged in a separate shell
    ```sh
       cd <iot-edge-path>/edgelet
       cargo run -p aziot-edged
    ```
11. When stopping the service, stop aziot-edged, identityd, keyd and certd, in that order.
### Run tests

```sh
cargo test --all
```

### Run Code Coverage Checks

In order to run Code Coverage Checks locally do the following 
```sh

#Run From the Edgelet Directory
cd edgelet 

#One Time Setup Only.
cargo install cargo-tarpaulin


#Run Unit Test with Code Coverage
cargo tarpaulin --out Xml --output-dir .
```

You should see an output like this

```sh
.
.
.
|| support-bundle/src/error.rs: 0/9
|| support-bundle/src/runtime_util.rs: 0/18
|| support-bundle/src/shell_util.rs: 0/117
|| support-bundle/src/support_bundle.rs: 0/50
|| 
46.28% coverage, 2993/6467 lines covered
```

Additionally, You can also view a HTML Report highlighting code sections covered using the following

```sh

#One Time Setup Only.
pip install pycobertura

#Create an HTML Report for viewing using pycobertura
pycobertura show --format html --output coverage.html cobertura.xml

```


### Additional Tools

- rustfmt

    This tool automatically formats the Rust source code. Our checkin gates assert that the code is correctly formatted.

    Install it with:

    ```sh
    cd edgelet/

    rustup component add rustfmt
    ```

    To format the source code, run:

    ```sh
    cargo fmt --all
    ```

    To verify the source code is already correctly formatted, run:

    ```sh
    cargo fmt --all -- --check
    ```

- clippy

    This is a Rust linter. It provides suggestions for more idiomatic Rust code and detects some common mistakes. Our checkin gates assert that clippy raises no warnings or errors when run against the code.

    Install it with:

    ```sh
    cd edgelet/

    rustup component add clippy
    ```

    Run it with:

    ```sh
    cargo clippy --all
    cargo clippy --all --tests
    cargo clippy --all --examples
    ```

- Swagger

    Some of our source code is generated from swagger definitions stored as YAML.

    You can edit the definitions in VS code, but https://editor.swagger.io is also an invaluable tool for validation, converting YAML -> JSON for code-gen, etc.

    We use a modified version of `swagger-codegen` to generate code from the swagger definitions. To build the tool:

    ```sh
    git clone -b support-rust-uds https://github.com/avranju/swagger-codegen.git
    cd swagger-codegen
    mvn clean package
    ```

    To run the tool, for example to update our workload API:

    ```sh
    java -jar swagger-codegen-cli.jar generate -i api/workload.yaml -l rust -o {root}/edgelet/workload
    ```

    Note that we've manually fixed up the generated code so that it satisfies rustfmt and clippy. As such, if you ever need to run `swagger-codegen-cli` against new definitions, or need to regenerate existing ones, you will want to perform the same fixups manually. Make sure to run clippy and rustfmt against the new code yourself, and inspect the diffs of modified files before checking in.

    For more details, please visit [**How to build Management API using Swagger-Codegen**](../api/README.md)

- IDE

    [VS Code](https://code.visualstudio.com/) has good support for Rust. Consider installing the following extensions:

    * [Rust (rls)](https://marketplace.visualstudio.com/items?itemName=rust-lang.rust) - Syntax highlighting and Intellisense support
    * [Better TOML](https://marketplace.visualstudio.com/items?itemName=bungcip.better-toml) - Syntax highlighting for `Cargo.toml`
    * [C/C++](https://marketplace.visualstudio.com/items?itemName=ms-vscode.cpptools) - Native debugger support
    * [Vim](https://marketplace.visualstudio.com/items?itemName=vscodevim.vim) - For a more sophisticated editor experience :)

    Alternatively, [IntelliJ IDEA Community Edition](https://www.jetbrains.com/idea/) with the [Rust plugin](https://intellij-rust.github.io/) provides a full IDE experience for programming in Rust.


### Test IoT Edge daemon API endpoints

If you would like to know how to test IoT Edge daemon API endpoints on dev machine, please read from [here](testiotedgedapi.md).


## References

* [The Rust Programming Language Book](https://doc.rust-lang.org/book/second-edition/index.html)

* [Rust API Guidelines](https://rust-lang.github.io/api-guidelines/) - Guidelines on naming conventions, organization, function semantics, etc.
