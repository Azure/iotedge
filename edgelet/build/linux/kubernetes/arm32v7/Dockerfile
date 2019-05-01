FROM azureiotedge/azureiotedge-iotedged-base:1.0-beta-arm32v7

WORKDIR /app
ADD libiothsm.so* /app/
ADD iotedged /app

ENV LD_LIBRARY_PATH /app

CMD ["/app/iotedged"]