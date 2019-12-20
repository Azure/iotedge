FROM dmolokanov/edgehub-proxy-base:1.0-arm32v7

COPY haproxy.cfg /usr/local/etc/haproxy/haproxy.cfg
COPY docker-entrypoint.sh /
COPY target/armv7-unknown-linux-gnueabihf/release/edgehub-proxy /usr/bin
