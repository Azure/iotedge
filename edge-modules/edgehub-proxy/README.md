# Edge Hub TLS Proxy

This module is a TCP Proxy for the Edge Hub, which gives control over the TLS settings.

## Building

Docker is required for building. Run the build script with the desired image tag and target architecture:

### amd64
```bash
./build.sh -i <image tag> -t x86_64
```

### arm32v7 
```bash
./build.sh -i <image tag> -t armv7l
```