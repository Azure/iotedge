FROM ubuntu:18.04

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
    python \
    debhelper \
    dh-systemd \
    pkg-config

COPY apt/cmake.sh /                                                       
RUN apt-get purge --auto-remove -y cmake && \                         
    bash /cmake.sh 3.11.4

ARG TRIPLE=aarch64-linux-gnu
ARG TOOLCHAIN=gcc-linaro-7.4.1-2019.02-x86_64_${TRIPLE}
COPY $TOOLCHAIN /toolchain/
ENV PATH="${PATH}:/toolchain/bin:/toolchain/${TRIPLE}/bin" \
    SYSROOT=/toolchain/${TRIPLE}/libc

COPY ubuntu18.04/openssl.sh apt/qemu.sh /                                             
RUN bash /openssl.sh linux-aarch64 ${TRIPLE}- ${SYSROOT} && \            
    bash /qemu.sh aarch64                                                 
      
COPY apt/zlib.sh /                                                       
RUN bash /zlib.sh 1.2.11 ${TRIPLE} ${SYSROOT}

COPY apt/curl.sh /                                                       
RUN bash /curl.sh 7.59.0 ${TRIPLE} ${SYSROOT}
ENV CARGO_TARGET_AARCH64_UNKNOWN_LINUX_GNU_LINKER=${TRIPLE}-gcc \
    CARGO_TARGET_AARCH64_UNKNOWN_LINUX_GNU_RUNNER=qemu-aarch64 \
    CC_aarch64_unknown_linux_gnu=${TRIPLE}-gcc \
    CXX_aarch64_unknown_linux_gnu=${TRIPLE}-g++ \
    OpenSSLDir=${SYSROOT}/usr \
    OPENSSL_DIR=${SYSROOT}/usr \
    OPENSSL_INCLUDE_DIR=${SYSROOT}/usr/include \
    OPENSSL_LIB_DIR=${SYSROOT}/usr/lib \
    QEMU_LD_PREFIX=$SYSROOT \
    RUST_TEST_THREADS=1 \
    PKG_CONFIG_ALLOW_CROSS=1

