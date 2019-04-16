Write-Host "$env:PATH"
Write-Host "$env:OPENSSL_DIR"
Write-Host "$env:OPENSSL_ROOT_DIR"

Write-Host "cargo.exe build --target thumbv7a-pc-windows-msvc --release"

# cmd /c "cargo.exe --help" | Write-Host
# cmd /c "cargo.exe clean -v" | Write-Host
Invoke-Expression "cargo.exe build --target thumbv7a-pc-windows-msvc --release -v"

if ($LastExitCode)
{
    Throw "cargo build failed with exit code $LastExitCode"
}
