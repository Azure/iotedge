#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------

ARG DIR=.

FROM nginx@sha256:088807753a80fae07f41d44e0e723d04f8fd8acee5d74bf929b926c3d90c10ab
WORKDIR /app
COPY ./docker/linux/arm32v7/api-proxy-module .
COPY ./docker/linux/arm32v7/templates .
EXPOSE 443/tcp	
EXPOSE 80/tcp
EXPOSE 5000/tcp
EXPOSE 11002/tcp
EXPOSE 7000-8000/tcp
ENTRYPOINT ["/app/api-proxy-module"]
 