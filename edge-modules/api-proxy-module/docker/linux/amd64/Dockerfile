#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------

ARG DIR=.

FROM amd64/nginx:1.19.3-alpine
WORKDIR /app
COPY ./docker/linux/amd64/api-proxy-module .
COPY ./docker/linux/amd64/templates .

#expose ports
EXPOSE 443/tcp	
EXPOSE 80/tcp
#used by registry
EXPOSE 5000/tcp
#used by blob storage
EXPOSE 11002/tcp
#use for custom defining ports
EXPOSE 7000-8000/tcp
ENTRYPOINT ["/app/api-proxy-module"]
 