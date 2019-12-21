FROM haproxy:1.8.14-alpine

RUN apk add --no-cache curl jq

RUN mkdir -p /run/haproxy

COPY haproxy.cfg /usr/local/etc/haproxy/haproxy.cfg

EXPOSE 8883 5671 443

COPY docker-entrypoint.sh /
COPY target/x86_64-unknown-linux-musl/release/edgehub-proxy /usr/bin
