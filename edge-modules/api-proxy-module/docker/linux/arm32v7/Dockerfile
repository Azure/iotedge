#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------

FROM arm32v7/nginx:1.19.7
WORKDIR /app
COPY ./docker/linux/arm32v7/api-proxy-module .
COPY ./docker/linux/arm32v7/templates .

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
 