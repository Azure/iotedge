
# Setting up a TPM device with iotedged access

## The problem

On Linux systems, the iotedged process normally runs as a user with limited 
privileges. By default this user will not have access to the TPM device. 

There are two basic ways to solve this problem.  The first and most obvious 
way is to run iotedged as root. If this is not desireable, then the other 
option is to allow the iotedge user access to the TPM device.

This document describes how we might set up a TPM device on a system running 
the iotedged as user iotedge:iotedge.  This will set up a udev service rule to 
give access to `/dev/tpm0` for the iotedge account.

## Find the TPM device

The first step is to locate the hardware in `/sys`. We are looking for the 
device that represents `/dev/tpm0`. We can confirm this is `/dev/tpm0` by 
running the `udevadm` command. For example:

```sh
# find /sys -name dev -print | fgrep tpm
/sys/devices/LNXSYSTM:00/LNXSYBUS:00/MSFT0101:00/tpm/tpm0/dev

# /bin/udevadm info -a -q all -p /sys/devices/LNXSYSTM:00/LNXSYBUS:00/MSFT0101:00/tpm/tpm0
P: /devices/LNXSYSTM:00/LNXSYBUS:00/MSFT0101:00/tpm/tpm0
N: tpm0
E: DEVNAME=/dev/tpm0
E: DEVPATH=/devices/LNXSYSTM:00/LNXSYBUS:00/MSFT0101:00/tpm/tpm0
E: MAJOR=10
E: MINOR=224
E: SUBSYSTEM=tpm
```

So, for this example, `/sys/devices/LNXSYSTM:00/LNXSYBUS:00/MSFT0101:00/tpm/tpm0` 
represents the device we want to access.

## Construct a udev rule for /dev/tpm0

The following `udevadm info` above has the information you might need to construct a rule for the udev service.

```sh
# /bin/udevadm info -a -p /sys/devices/LNXSYSTM:00/LNXSYBUS:00/MSFT0101:00/tpm/tpm0

Udevadm info starts with the device specified by the devpath and then
walks up the chain of parent devices. It prints for every device
found, all possible attributes in the udev rules key format.
A rule to match, can be composed by the attributes of the device
and the attributes from one single parent device.

  looking at device '/devices/LNXSYSTM:00/LNXSYBUS:00/MSFT0101:00/tpm/tpm0':
    KERNEL=="tpm0"
    SUBSYSTEM=="tpm"
    DRIVER==""

  looking at parent device '/devices/LNXSYSTM:00/LNXSYBUS:00/MSFT0101:00':
    KERNELS=="MSFT0101:00"
    SUBSYSTEMS=="acpi"
    DRIVERS=="tpm_crb"
    ATTRS{description}=="TPM 2.0 Device"
    ATTRS{hid}=="MSFT0101"
    ATTRS{path}=="\_SB_.TPM_"
    ATTRS{status}=="15"

  looking at parent device '/devices/LNXSYSTM:00/LNXSYBUS:00':
    KERNELS=="LNXSYBUS:00"
    SUBSYSTEMS=="acpi"
    DRIVERS==""
    ATTRS{hid}=="LNXSYBUS"
    ATTRS{path}=="\_SB_"

  looking at parent device '/devices/LNXSYSTM:00':
    KERNELS=="LNXSYSTM:00"
    SUBSYSTEMS=="acpi"
    DRIVERS==""
    ATTRS{hid}=="LNXSYSTM"
    ATTRS{path}=="\"
```

So, for the TPM device we want to use, the KERNEL value is "tpm0" and the 
SUBSYSTEM value is "tpm".  These are the matching criteria for the rule we 
will create.

We want to allow access to `/dev/tpm0` to iotedged, which is running as user 
iotedge. We could set ownership of the device to this user, or leave the 
ownership of the device and give group privileges to the device. In this 
example, we set the device to allow iotedge group access.

The new rules file should look like this:

```
# allow iotedge access to tpm0

KERNEL=="tpm0", SUBSYSTEM=="tpm", GROUP="iotedge", MODE="0660"
```

Udev rules may be placed in `udev/rules.d` directory under `/etc`, `/lib`, or 
`/run`. The name of the file must end in ".rules" but can be named anything. 
The filenames are gathered and processed in lexical order.  See the udev\(7\) 
manpage for details.

Once the new rules file is in place, the udev service needs to evaulate the 
rule. To make the udev service act on this rule without a reboot, you can run:

```sh
# /bin/udevadm trigger /sys/devices/LNXSYSTM:00/LNXSYBUS:00/MSFT0101:00/tpm/tpm0
```

Once that is complete, we can confirm the device is accessible to our iotedge user:
```sh
$ ls -l /dev/tpm0
crw-rw---- 1 root iotedge 10, 224 May 31 15:13 /dev/tpm0
```

Other suggested reading:
[Create and provision a simulated TPM device using C device SDK for IoT Hub Device Provisioning Service](https://docs.microsoft.com/en-us/azure/iot-dps/quick-create-simulated-device)
