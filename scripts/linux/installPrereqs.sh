#!/bin/bash

sudo apt-get update
sudo apt-get install -y libsnappy1v5 libc6-dev

# RocksDbSharp v5.17.2 expects to use libdl.so to load the native rocksdb library, but that library only exists as
# libdl.so.2 in later versions of Ubuntu (22.04+). To work around this problem when running the unit tests, we create
# a symlink.
libdl_path=$(find /usr/lib -name 'libdl.so')
libdl2_path=$(find /usr/lib -name 'libdl.so.2')
if [[ -z "$libdl_path" && -n "$libdl2_path" ]]; then
    sudo ln -s $libdl2_path $(dirname $libdl2_path)/libdl.so
fi

exit 0
