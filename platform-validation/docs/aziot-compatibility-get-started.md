
# Platform Compatibility Script

IoT Edge Compatibility script performs many checks to figure out whether a platform has necessary capabilities to run IoT Edge. The script is useful when one has to find if IoT Edge can run in a custom-built OS other than the one listed in [supported IoT Edge platforms](https://docs.microsoft.com/en-us/azure/iot-edge/support?view=iotedge-2020-11). The recommendation is to run this script before installing IoT edge to ensure if the platform has all the capabilities to get started. 

## List of Checks

Following are the checks that are performed in the Platform Compatibility script. 

### Root User
* To ensure the script has root privileges to perform certain operations. 
### Control Groups
* To ensure cgroups hierarchy exist in the current platform. 
### Kernel Flags
* The following kernel flags are checked for various capabilities that are necessary for the full functionality of IoT Edge.
	* EXT4_FS_SECURITY
	* NAMESPACES 
	* NET_NS
	* PID_NS
	* IPC_NS UTS_NS
	* CGROUPS
	* CGROUP_CPUACCT 
	* CGROUP_DEVICE
	* CGROUP_FREEZER
	* CGROUP_SCHED CPUSETS MEMCG
	* KEYS
	* VETH BRIDGE BRIDGE_NETFILTER
	* IP_NF_FILTER
	* IP_NF_TARGET_MASQUERADE
	* NETFILTER_XT_MATCH_ADDRTYPE 
	* NETFILTER_XT_MATCH_CONNTRACK
	* NETFILTER_XT_MATCH_IPVS
	* NETFILTER_XT_MARK
	* IP_NF_NAT NF_NAT 
	* POSIX_MQUEUE
### Systemd
* Presence of systemd is needed to manage IoT edge services. Its absence alerts to manage them separately.
### Supported Architecture
* IoT Edge does not support all architecture and the check evaluates if the current platform architecture is supported or not.
### Container Engine Client API
* Currently the minimum verion of the Docker engine client API is checked.
### Shared Library Dependency
* IoT Edge has dependency on shared libraries that are part of the OS distro. This check evaluates if the current platform's OS has all the required libraires. Example libraries are `libssl.so.1.1` , `libcrypto.so.1.1` etc.
### Storage Space
* To ensure enough storage space exists in the platform which is needed for the IoT edge binaries and a simple workload of IoT edge containers.
### Memory Space
* To ensure enough memory exists in the platform which is needed for the IoT edge binaries and a simple workload of IoT edge containers.
### Package Managers and CA certificates
* To ensure the platform has supported package managers like `apt-get`, `dnf`, `yum`,`dpkg` and `rpm`. The high level package managers like `apt-get` , `dnf` and `yum` ensures to install the additional packages like ca-certificates. So an additional check is performed with packages managers like `dpkg` and `rpm` to ensure `ca-certificates` in installed.

## Usage
 
 * To download the script, use the following command
 
`wget aka.ms/aziot-compat-prev`

* To run the script, use the following commands for help.

`./aziot-compatibilty --help` or `./aziot-compatibilty -h`

* To list the apps that are supported by the Compatibility script

`./aziot-compatibilty --list-apps` or `./aziot-compatibilty -l`

* To enable the debug logs of the script 
`./aziot-compatibilty -v` or `./aziot-compatibilty --verbose`

* To know the version of the IoT edge supported by the Compatibility script
`./aziot-compatibilty --app-version`


## Output

The Compatibility script outputs the results in the following categories

* Successful - Checks that are passed
* Errors - Checks that have failed 
* Warning - Checks that have warning messages for alerts of action.
* Skipped - Checks that have been skipped