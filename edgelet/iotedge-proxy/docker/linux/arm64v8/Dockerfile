FROM arm64v8/alpine:3.10

RUN addgroup -S proxy && adduser -S proxy -G proxy
USER proxy

WORKDIR /app
COPY ./docker/linux/arm64v8/iotedge-proxy /app/iotedge-proxy

CMD ["/app/iotedge-proxy"]
