# syntax=docker/dockerfile:1.4

#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------

FROM alpine:3.16
WORKDIR /app

RUN	apk update && \
    apk add --no-cache libcap

ENV NGINXUSER_ID ${NGINXUSER_ID:-13624}
RUN adduser -Ds /bin/sh -u ${NGINXUSER_ID} nginx

RUN chown -R nginx:nginx /app

COPY ./docker/linux/amd64/api-proxy-module .
COPY ./docker/linux/amd64/templates .

RUN	apk update && \
    apk add --no-cache nginx && \
	mkdir -p /run/nginx
RUN setcap 'cap_net_bind_service=+ep' /usr/sbin/nginx

RUN chown -R nginx:nginx /app && \
    mkdir /var/cache/nginx && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx
RUN touch /var/run/nginx/nginx.pid && \
        chown -R nginx:nginx /var/run/nginx/nginx.pid


USER nginx

#expose ports
EXPOSE 443/tcp
EXPOSE 80/tcp
#used by registry
EXPOSE 5000/tcp
#used by blob storage
EXPOSE 11002/tcp
#default
EXPOSE 8000/tcp
ENTRYPOINT ["/app/api-proxy-module"]
