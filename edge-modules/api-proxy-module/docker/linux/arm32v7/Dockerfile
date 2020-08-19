#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------

ARG DIR=.

FROM alpine:3.12.0
WORKDIR /app
COPY target/x86_64-unknown-linux-musl/release/api-proxy-module .
COPY templates .
RUN ls
RUN	apk update && \
    apk add nginx && \
	mkdir /run/nginx
ENTRYPOINT ["/app/api-proxy-module"]
 