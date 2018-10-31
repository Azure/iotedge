FROM centos:centos7.5.1804
ARG TOOLCHAIN="stable"

RUN yum group install -y "Development Tools" && \
    yum install -y redhat-rpm-config \
        git \
        openssl-devel \
        libcurl-devel

COPY yum/cmake.sh /
RUN bash /cmake.sh 3.11.4

ARG TRIPLE=aarch64-linux-gnu
ARG TOOLCHAIN=gcc-linaro-7.3.1-2018.05-x86_64_${TRIPLE}
COPY $TOOLCHAIN /toolchain/
ENV PATH="${PATH}:/toolchain/bin:/toolchain/${TRIPLE}/bin" \
    SYSROOT=/toolchain/${TRIPLE}/libc

COPY centos/openssl.sh yum/qemu.sh /
RUN bash /openssl.sh linux-armv4 ${TRIPLE}- ${SYSROOT} && \
    bash /qemu.sh arm

COPY yum/zlib.sh /
RUN bash /zlib.sh 1.2.11 ${TRIPLE} ${SYSROOT}

COPY yum/curl.sh /
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

