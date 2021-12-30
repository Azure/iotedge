# docker file for azureiotedge/azureiotedge-runtime-base:1.2-linux-arm32v7
FROM ubuntu:18.04

ARG num_procs=4

# cross-compile arm32 for bionic
RUN sources="$(cat /etc/apt/sources.list | grep -E '^[^#]')"  && \ 
    echo "$sources" | sed -e 's/^deb /deb [arch=amd64] /g' > /etc/apt/sources.list &&\
    echo "$sources" | sed -e 's/^deb /deb [arch=armhf] /g' \
      -e 's|http://archive.ubuntu.com/ubuntu/|http://ports.ubuntu.com/ubuntu-ports/|g' \
      -e 's|http://security.ubuntu.com/ubuntu/|http://ports.ubuntu.com/ubuntu-ports/|g' \
      >> /etc/apt/sources.list
    
RUN apt-get update && \
    dpkg --add-architecture armhf &&\
    apt-get install -y make git \
      gcc-8-arm-linux-gnueabihf libsnappy-dev:armhf g++-8-arm-linux-gnueabihf 

RUN git clone -b 'v5.17.2' --depth 1 'https://github.com/facebook/rocksdb' &&\
    cd rocksdb &&\
    CC=arm-linux-gnueabihf-gcc-8 \
      CXX=arm-linux-gnueabihf-g++-8 \
      AR=arm-linux-gnueabihf-ar-8 \
      LIBEXT=.so \
      CFLAGS='-static-libstdc++ -DSNAPPY' \
      PORTABLE=1 \
      make -j8 shared_lib &&\
    arm-linux-gnueabihf-strip ./librocksdb.so &&\
    mkdir -p ../publish &&\
    cp -vL ./librocksdb.so ../publish/librocksdb.so.armhf
