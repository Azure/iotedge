FROM debian:8.11-slim

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    git \
    software-properties-common \
    build-essential \
    ca-certificates \
    uuid-dev \
    curl \
    openssl \
    libssl-dev \
    libcurl4-openssl-dev \
    debhelper \
    dh-systemd \
    pkg-config

COPY apt/cmake.sh /                                                       
RUN apt-get purge --auto-remove -y cmake && \                         
    bash /cmake.sh 3.11.4

ARG TOOLCHAIN=gcc-linaro-7.3.1-2018.05-x86_64_arm-linux-gnueabihf
ARG TRIPLE=arm-linux-gnueabihf
COPY $TOOLCHAIN /toolchain/
ENV PATH="${PATH}:/toolchain/bin:/toolchain/${TRIPLE}/bin" \
    SYSROOT=/toolchain/${TRIPLE}/libc

COPY debian8/openssl.sh apt/qemu.sh /                                             
RUN bash /openssl.sh linux-armv4 ${TRIPLE}- ${SYSROOT} && \            
    bash /qemu.sh arm                                                 
      
COPY apt/zlib.sh /                                                       
RUN bash /zlib.sh 1.2.11 ${TRIPLE} ${SYSROOT}

COPY debian8/curl.sh /                                                       
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
