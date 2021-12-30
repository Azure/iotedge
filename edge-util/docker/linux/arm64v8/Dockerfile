# docker file for azureiotedge/azureiotedge-runtime-base:1.2-linux-arm64v8
FROM ubuntu:18.04

ARG num_procs=4

# cross-compile arm32 for bionic
RUN sources="$(cat /etc/apt/sources.list | grep -E '^[^#]')"  && \ 
    echo "$sources" | sed -e 's/^deb /deb [arch=amd64] /g' > /etc/apt/sources.list &&\
    echo "$sources" | sed -e 's/^deb /deb [arch=arm64] /g' \
      -e 's|http://archive.ubuntu.com/ubuntu/|http://ports.ubuntu.com/ubuntu-ports/|g' \
      -e 's|http://security.ubuntu.com/ubuntu/|http://ports.ubuntu.com/ubuntu-ports/|g' \
      >> /etc/apt/sources.list
    
RUN apt-get update && \
    dpkg --add-architecture arm64 &&\
    apt-get install -y make git \
      gcc-8-aarch64-linux-gnu g++-8-aarch64-linux-gnu libsnappy-dev:arm64 

RUN git clone -b 'v5.17.2' --depth 1 'https://github.com/facebook/rocksdb' &&\
    cd rocksdb &&\
    CC=aarch64-linux-gnu-gcc-8 \
      CXX=aarch64-linux-gnu-g++-8 \
      AR=aarch64-linux-gnu-ar-8 \
      LIBEXT=.so \
      CFLAGS='-static-libstdc++ -DSNAPPY' \
      PORTABLE=1 \
      make -j8 shared_lib && \
    aarch64-linux-gnu-strip ./librocksdb.so && \
    mkdir -p ../publish &&\
    cp -vL ./librocksdb.so ../publish/librocksdb.so.arm64
