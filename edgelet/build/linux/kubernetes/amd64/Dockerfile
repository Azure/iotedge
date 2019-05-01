FROM ubuntu:18.04

RUN apt-get update && apt-get install -y \
  libssl1.0.0 \
  ca-certificates \
  && rm -rf /var/lib/apt/lists/*

WORKDIR /app
ADD libiothsm.so* /app/
ADD iotedged /app

ENV LD_LIBRARY_PATH /app

CMD ["/app/iotedged"]
