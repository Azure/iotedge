
# Profiling of IoT Edge

IoT Edge Compatibility script performs many checks to figure out whether a platform has capabilities to run IoT Edge or not. This document is focused on how the profiling of IoT Edge is performed and how the profiling information is used to perform the memory and storage checks in the Compatibility script for platform validation.

## Setup

IoT Edge binaries like `aziot-edged`, `aziot-idenitityd` are installed in three different platforms i.e x86_64, aarch64 and armv7l with Ubuntu 20.04.

A simple workload of Edge RunTime Containers (edgeAgent and edgeHub) along with SimulatedTemperatureSensor module were deployed. The profiling has been done by running the workload for a duration of **one hour**.

Note: The metrics would drastically vary if the workload would have more container workloads other than the one specified in the setup. The version of the binaries and containers of IoT Edge are mentioned in the table below.

  

| Name | Version |
|:-----:|:-------:|
| IoT Edge Service| 1.2.8|
| IoT Identity service | 1.2.6 |
| EdgeAgent | 1.2 |
| EdgeHgent | 1.2 |
| SimulatedTemperatureSensor | 1.2 |

  The specifications of the three platforms are in the table below.

| Platform | Processor | vCPU| RAM(GiB) |  Disk (GiB) 
|:----------|:---------:|:--------:|:--------:|:--------:|
| armv7l| Raspberry Pi 3 Model B Plus Rev 1.3 @1.2 GHz Quad core  BCM2835 ARMv7 Processor | 1 | 1 | 8 |
| aarch64 | TBD | 2 | 8 | 75 
| x86_64 |Intel Xeon E5-2673 v4 @ 2.29 GHz 1 Processor, 1 Core, 2 Threads|  2 | 8 | 16 |

## Metrics

Two major metrics are observed from this  i.e memory and storage. In both the categories the binary and container size is profiled.

### Memory check

* Memory Usage (Resisent Set Size) of Binaries

	* The average dynamic memory consumed by the IoT edge binaries like aziot-edged, aziot-identityd, aziot-certd etc and it is obtained by using `ps` command.

* Memory Usage (Resisent Set Size) of Containers

	* The average dynamic memory consumed by the a simple workload of Edge runtime Containers (edgeAgent and edgeHub) along with SimulatedTemperatureSensor  and it is obtained by `docker stats` command.


### Storage check

* On Disk Binary Size

	* The storage space consumed by the IoT edge binaries like aziot-edged, aziot-identityd, aziot-certd etc and it is obtained by calculating the binaries size using `stat` command in the system.

* On Disk Container Size

	* The storage space consumed by the IoT edge workload containers like edgeAgent, edgeHub and SimulatedTemperatureSensor and it is obtained by `docker inspect` command. Only size of the unique layers of the containers are considered for this metric.

A summary of the metrics by profiling specified version of IoT edge in three platforms are as follows. The sizes are in **MB**

| Metric Name |x86_64 |armv7l |aarch64 |
|:-----------------------:|:----------:|:----------:|:----------:|
|Memory Usage (Resisent Set Size) of Binaries |55 |36 |27 |
|Memory Usage (Resisent Set Size) of Containers |175 |164|210|
|On Disk Binary Size |43 |37 |37 |
|On Disk Container Size |255 |323 |323 |

## Analysis

The four metrics are used by the IoT Edge compatibility script to evaluate whethe the platform has enough memory and storage to run IoT edge. The compatibility script queries the `/proc/meminfo` file in the system to evaluate the memory checks and `df` command to evaluate the storage checks.profiling