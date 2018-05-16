
FROM ubuntu:12.04

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    git \
    build-essential \
    ca-certificates \
    cmake \
    uuid-dev \
    curl \
    openssl \
    libssl-dev \
    libcurl4-openssl-dev \
    pkg-config

COPY xargo.sh /                                                       
RUN bash /xargo.sh                                                    

COPY cmake.sh /                                                       
RUN apt-get purge --auto-remove -y cmake && \                         
    bash /cmake.sh 3.0.2

ARG TOOLCHAIN=gcc-linaro-7.2.1-2017.11-x86_64_arm-linux-gnueabihf
ARG TRIPLE=arm-linux-gnueabihf
COPY $TOOLCHAIN /
ENV PATH="${PATH}:/${TRIPLE}/bin" \
    SYSROOT=/${TRIPLE}/libc

COPY openssl.sh qemu.sh /                                             
RUN bash /openssl.sh linux-armv4 ${TRIPLE}- ${SYSROOT} && \            
    bash /qemu.sh arm                                                 
      
COPY zlib.sh /                                                       
RUN bash /zlib.sh 1.2.11 ${TRIPLE} ${SYSROOT}

COPY curl.sh /                                                       
RUN bash /curl.sh 7.59.0 ${TRIPLE} ${SYSROOT}

ENV CARGO_TARGET_ARMV7_UNKNOWN_LINUX_GNUEABIHF_LINKER=${TRIPLE}-gcc \
    CARGO_TARGET_ARMV7_UNKNOWN_LINUX_GNUEABIHF_RUNNER=qemu-arm \
    CC_armv7_unknown_linux_gnueabihf=${TRIPLE}-gcc \
    CXX_armv7_unknown_linux_gnueabihf=${TRIPLE}-g++ \
    OpenSSLDir=${SYSROOT}/usr \
    OPENSSL_DIR=${SYSROOT}/usr \
    OPENSSL_INCLUDE_DIR=${SYSROOT}/usr/include \
    OPENSSL_LIB_DIR=${SYSROOT}/usr/lib \
    QEMU_LD_PREFIX=$SYSROOT \
    RUST_TEST_THREADS=1 \
    PKG_CONFIG_ALLOW_CROSS=1
