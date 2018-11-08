FROM haproxy:1.8.14-alpine

RUN apk add --no-cache curl jq

RUN mkdir -p /run/haproxy

COPY haproxy.cfg /usr/local/etc/haproxy/haproxy.cfg

EXPOSE 8883 5671 443

COPY entrypoint.sh /
ENTRYPOINT ["/entrypoint.sh"]
CMD ["haproxy", "-f", "/usr/local/etc/haproxy/haproxy.cfg"]
