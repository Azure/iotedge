FROM azureiotedge/azureiotedge-iotedged-base:1.0-linux-arm64v8

WORKDIR /app
ADD ./docker/linux/arm64v8/libiothsm.so* /app/
ADD ./docker/linux/arm64v8/iotedged /app

ENV LD_LIBRARY_PATH /app

CMD ["/app/iotedged"]
