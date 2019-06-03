FROM azureiotedge/azureiotedge-iotedged-base:1.0-arm32v7

WORKDIR /app
ADD ./docker/linux/arm32v7/libiothsm.so* /app/
ADD ./docker/linux/arm32v7/iotedged /app

ENV LD_LIBRARY_PATH /app

CMD ["/app/iotedged"]