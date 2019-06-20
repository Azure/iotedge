# IoT Edge Security Daemon - Dev Guide

## Building
The daemon is written in [Rust](https://www.rust-lang.org/en-US/). Rust uses two cli tools for its build: rustup and cargo.

### Rustup
Rustup is rust's toolchain manager. Rust ships stable, beta, and nightly versions of the compiler and standard library.
This project uses stable, but some of the tooling uses the nightly compiler. Rustup is used to update the compiler, and
switch the active toolchain. You will use rustup infrequently.

To install rustup, visit [https://www.rustup.rs](https://www.rustup.rs/) and follow the instructions.

To install or update the compiler:
```
rustup update stable
```

### Additional Dependencies

#### Linux

```bash
sudo apt-get update
sudo apt-get install -y git cmake build-essential curl libcurl4-openssl-dev libssl-dev uuid-dev pkg-config
```

### macOS

1. Install necessary tools for development using [Homebrew](https://brew.sh/) package manager
    ```bash
    brew update
    brew install cmake openssl
    ```

1. Set `OPENSSL_ROOT_DIR`
    ```bash
    export OPENSSL_ROOT_DIR=/usr/local/opt/openssl
    export OPENSSL_DIR=/usr/local/opt/openssl
    ```

#### Windows

1. Install `vcpkg`

	```powershell
	git clone https://github.com/Microsoft/vcpkg
	cd vcpkg
	.\bootstrap-vcpkg.bat
	```

1. Install openssl binaries

	```powershell
	.\vcpkg install openssl:x64-windows
	```

1. Set `OPENSSL_ROOT_DIR`

	```powershell
	$env:OPENSSL_ROOT_DIR = "$PWD\installed\x64-windows"
	$env:OPENSSL_DIR = "$PWD\installed\x64-windows"
	```

### Cargo
Cargo is the build tool for rust. You will use cargo frequently. It manages the build of the project, downloading dependencies,
testing, etc. You can read more about cargo and it's capabilities in the [cargo book](https://doc.rust-lang.org/cargo/).

#### Building
To build the project, use:
```
cargo build --all
```

#### Testing
To test the project, use:
```
cargo test --all
```

### Additional Tools
Rust has a few tools that help in day to day development.

Note: on older installations of Rust, these components may not be available.  You will need to update rustup, and reinstall the stable toolchain:
```
rustup self update
rustup uninstall stable
rustup install stable
```

#### Cargo Fmt
Cargo supports a formatting tool to automatically format the source code.

Install it with:
```
rustup component add rustfmt
```

Run it with:
```
cargo fmt --all
```

By default, this will update the source files with newly formatted source files. Cargo fmt is also run as a checkin
gate to prevent code from being checked in that doesn't meet the style guidelines.

#### Cargo Clippy
Clippy is a linting tool for rust. It provides suggestions for more idiomatic rust code.

Install it with:
```
rustup component add clippy
```

Run it with:
```
cargo clippy --all
cargo clippy --all --tests
cargo clippy --all --examples
```

Clippy is also run as a checkin gate.

### Swagger

#### Definitions

We use YAML for our swagger definitions. You can edit the definitions in VS code, but https://editor.swagger.io is also an invaluable tool for validation, converting YAML -> JSON for code-gen, etc.

#### Code generation

We use a modified version of `swagger-codegen` to generate code from our swagger definitions. To build the tool:

```
git clone -b support-rust-uds  https://github.com/avranju/swagger-codegen.git
cd swagger-codegen
mvn clean package
```

To run the tool, for example to update our workload API:

```
java -jar swagger-codegen-cli.jar generate -i api/workload.yaml -l rust -o {root}/edgelet/workload
```

> Note that `cargo fmt` and `cargo clippy` will complain about the code produced by this tool. We like to keep **all** code in our repo clean, so you'll want to run clippy and fmt over the generated code, make the recommended changes, and carefully inspect the diffs of modified files before checking in.

## IDE
VS Code has good support for rust. Consider installing the following extensions:

* [Rust](https://marketplace.visualstudio.com/items?itemName=rust-lang.rust) - Syntax highlighting and intellisense support
* [Better TOML](https://marketplace.visualstudio.com/items?itemName=bungcip.better-toml) - Syntax highlighting for Cargo.toml
* [C/C++](https://marketplace.visualstudio.com/items?itemName=ms-vscode.cpptools) - Native debugger support
* [Vim](https://marketplace.visualstudio.com/items?itemName=vscodevim.vim) - For a more sophisticated editor experience :)

There is a `launch.json` configuration in this repo to setup debugging on Windows. This should work out of the box.

## Test IoT Edge daemon API endpoints on dev machine
If you would like to know how to test IoT Edge daemon API endpoints on dev machine, please read from [here](testiotedgedapi.md).

## Other

* [The Book](https://doc.rust-lang.org/book/second-edition/index.html) - The Rust Programming Language
* [RUST Api Guidelines](https://rust-lang-nursery.github.io/api-guidelines/) - Guidelines on naming conventions, organization, etc.
