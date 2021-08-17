#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------

FROM alpine:3.13.1
WORKDIR /app

RUN adduser -Ds /bin/sh nginx 
RUN chown -R nginx:nginx /app   

COPY ./docker/linux/amd64/api-proxy-module .
COPY ./docker/linux/amd64/templates .

RUN	apk update && \
    apk add nginx && \
	mkdir /run/nginx

RUN chown -R nginx:nginx /app && \
    mkdir /var/cache/nginx && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    chown -R nginx:nginx /etc/nginx/conf.d
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
