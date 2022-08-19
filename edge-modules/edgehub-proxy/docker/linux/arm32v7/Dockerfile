# syntax=docker/dockerfile:1.4

FROM azureiotedge/edgehub-proxy-base:1.0-linux-arm32v7

COPY haproxy.cfg /usr/local/etc/haproxy/haproxy.cfg
COPY docker-entrypoint.sh /
COPY target/armv7-unknown-linux-gnueabihf/release/edgehub-proxy /usr/bin
