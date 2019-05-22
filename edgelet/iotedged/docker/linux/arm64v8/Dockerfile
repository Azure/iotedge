FROM arm64v8/debian:9

RUN apt-get update && apt-get install -y \
  libssl1.0.2 \
  ca-certificates \
  && rm -rf /var/lib/apt/lists/*

WORKDIR /app
ADD ./docker/linux/arm64v8/libiothsm.so* /app/
ADD ./docker/linux/arm64v8/iotedged /app

ENV LD_LIBRARY_PATH /app

CMD ["/app/iotedged"]