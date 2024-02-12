# syntax=docker/dockerfile:1.4

#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------
FROM arm32v7/nginx:1.25
WORKDIR /app

RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y libcap2-bin && \
    rm -rf /var/lib/apt/lists/*
RUN setcap 'cap_net_bind_service=+ep' /usr/sbin/nginx

COPY ./docker/linux/arm32v7/api-proxy-module .
COPY ./docker/linux/arm32v7/templates .

ENV NGINXUSER_ID ${NGINXUSER_ID:-13624}
RUN usermod -u ${NGINXUSER_ID} nginx

RUN chown -R nginx:nginx /app && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx
RUN touch /var/run/nginx.pid && \
        chown -R nginx:nginx /var/run/nginx.pid

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
