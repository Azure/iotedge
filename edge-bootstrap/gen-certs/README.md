# Edge Runtime Certificate Generation

The script gen-certs assists a user in creating self signed certificates
which can be used to bootstrap the Edge runtime environment.

## Overall Block Diagram:
```
                                      Output

+--------------+                 +--------------+
|              |                 |    Edge      | CA Certificate To Be
|    Root      |     Merge       |   Runtime    | Installed In Edge Module
|     CA       +----------------->  CA Chain    | And/Or Leaf Device
| Certificate  |                 | Certificate  | Certificate Store
|              |                 |              |
+------+-------+                 +-------^------+
       |                                 |
       |       +--------------+          |
       |       |   Device     |          | Append
       |       | Intermediate |          |
  Sign +------->     CA       +----------+
               | Certificate  |
               |              |
               +------+-------+       Output
                      |
                      |          +--------------+
                      |          |    Edge      | Server Certificate
                      |          |   Runtime    | Installed in Edge Hub
                 Sign +---------->    Server    | Module
                                 | Certificate  |
                                 |              |
                                 +--------------+

```

## Dependencies:
1. Bash on Linux, Powershell on Windows
2. OpenSSL
3. Root/Sudo/Admin access to install the CA chain certificate on leaf devices.

# How To Run:
    Linux:   ./generate-certs.sh --help

# How To Configure Certificate Generation:
Standard OpenSSL configuration files for the root, intermediate and server certificates are in the openssl*_ca.cnf files. Users can choose to review and update as needed.

# Outputs:
The following certificates and artifacts will be created relative to a user supplied output directory '$CERTSDIR'.

**Root CA**
* Private Key: $CERTSDIR/root/ca/private/azure-iot-edge.root.ca.key.pem
* CA Cert: $CERTSDIR/root/ca/certs/azure-iot-edge.root.ca.cert.pem

**Device Intermediate CA**
* Private Key: $CERTSDIR/device/ca/private/azure-iot-edge.device.ca.key.pem
* CA Cert: $CERTSDIR/device/ca/certs/azure-iot-edge.device.ca.cert.pem
* Device Chain CA: $CERTSDIR/output/azure-iot-edge.chain.ca.pem.crt

**Edge Runtime (Hub) Certificates**
* Private Key: $CERTSDIR/device/ca/private/azure-iot-edge.hub.server.key.pem
* Server Certificate: $CERTSDIR/output/azure-iot-edge.hub.server.cert.pfx