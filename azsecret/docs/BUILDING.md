1. Essentials:

  - Git
  - Rust

1. Clone the repository:

  ```
  git clone --recurse-submodules https://github.com/R25l84IHeXjIxy6HO1QXn0y0Dq9mt8EN/azsecret
  cd azsecret
  ```

1. Install Rust CLI tools:

  - `bindgen`: `cargo install bindgen`
  - `cbindgen`: `cargo install cbindgen`

1. Install system tools:

  - CMake
  - Clang + LLVM
    - `clang`
    - `libclang`
    - `llvm-config`
  - GCC
  - `make`
  - OpenSSL
  - `pkg-config`

1. Build

  ```
  cargo build
  ```