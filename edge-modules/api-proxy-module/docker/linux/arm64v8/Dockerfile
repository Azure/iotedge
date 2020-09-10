#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------

ARG DIR=.

FROM nginx@sha256:3a85694add68a9e7298d13627007340d17e5da9dcf172c89fc2a111eee805ca7
WORKDIR /app
COPY ./docker/linux/arm64v8/api-proxy-module .
COPY ./docker/linux/arm64v8/templates .
EXPOSE 443/tcp	
EXPOSE 80/tcp
EXPOSE 5000/tcp
EXPOSE 11002/tcp
EXPOSE 7000-8000/tcp
ENTRYPOINT ["/app/api-proxy-module"]
 