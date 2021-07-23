#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------

FROM alpine:3.13.1
WORKDIR /app
COPY ./docker/linux/amd64/api-proxy-module .
COPY ./docker/linux/amd64/templates .
RUN	apk update && \
    apk add nginx && \
	mkdir /run/nginx

RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser    

#expose ports
EXPOSE 443/tcp	
EXPOSE 80/tcp
#used by registry
EXPOSE 5000/tcp
#used by blob storage
EXPOSE 11002/tcp

ENTRYPOINT ["/app/api-proxy-module"]