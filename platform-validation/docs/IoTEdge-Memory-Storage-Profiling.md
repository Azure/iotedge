# Memory and Storage Profiling of IoT Edge

IoT Edge Compatibility script performs many checks to figure out whether a platform has capabilities to run IoT Edge or not. This document is focused on how the profiling of IoT Edge is performed and how the profiling information is used to perform the memory and storage checks in the Compatibility script for platform validation.

## Setup

IoT Edge binaries like `aziot-edged`, `aziot-idenitityd` are installed in three different platforms like x86_64, aarch64  and armv7l with Ubuntu 20.04.
A simple workload of three containers like edgeAgent, edgeHub and SimulatedTemperatureSensor were deployed. The profiling has been done by running the workload for a duration of **one hour**.
Note: The metrics would drastically vary if the workload would have more container workloads other than the one specified in the setup. The version of the binaries and containers of IoT Edge are mentioned in the table below.

|  Name | Version |
|:-----:|:-------:|
| IoT Edge Service| 1.2|
| IoT Identity service | 1.2 |
| EdgeAgent | 1.2 |
| EdgeHgent | 1.2 |
| SimulatedTemperatureSensor | 1.2 |


## Metrics 
Two major metrics are observed from this profiling i.e memory and storage. In both the categories the binary and container size is profiled. 

### Memory check
* On Memory Binary Size
	* The average dynamic memory consumed by the IoT edge binaries like aziot-edged, aziot-identityd, aziot-certd etc and it is obtained by using `ps` command.
* On Memory Container Size
	* The average dynamic memory consumed by the IoT edge workload containers like edgeAgent, edgeHub and SimulatedTemperatureSensor and it is obtained by `docker stats` command.

### Storage check
* On Disk Binary Size
	* The average storage space consumed by the IoT edge binaries like aziot-edged, aziot-identityd, aziot-certd etc and it is obtained by calculating the binaries size using `stat` command in the system.
* On Disk Container Size
	* The average storage space consumed by the IoT edge workload containers like edgeAgent, edgeHub and SimulatedTemperatureSensor and it is obtained by `docker inspect` command. Only size of the unique layers of the containers are considered for this metric.

A summary of the metrics by profiling specified version of IoT edge in three platforms are as follows. The sizes are in **MB**

|                         |x86_64      |armv7l      |aarch64     |              
|:-----------------------:|:----------:|:----------:|:----------:|
|On Memory Binary Size    |54.24       |35.51       |26.62       |
|On Memory Container Size |175.00      |164.53      |210.00      |
|On Disk Binary Size      |42.39       |36.68       |36.68       |
|On Disk Container Size   |254.96      |322.98      |322.60      |

## Analysis

The four metrics are used by the IoT Edge compatibility script to evaluate whethe the platform has enough memory and storage to run IoT edge. A buffer size of **50 MB** is observed with the current metrics. The compatibility script queries the `/proc/meminfo` file in the system to evaluate the memory checks and `df` command to evaluate the storage checks. 