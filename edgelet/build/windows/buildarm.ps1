Write-Host "$env:PATH"
Write-Host "$env:OPENSSL_DIR"
Write-Host "$env:OPENSSL_ROOT_DIR"

Write-Host "cargo.exe build --target thumbv7a-pc-windows-msvc --release"

Invoke-Expression "cargo.exe build --target thumbv7a-pc-windows-msvc --release" | Write-Host

if ($LastExitCode)
{
    Throw "cargo build failed with exit code $LastExitCode"
}
