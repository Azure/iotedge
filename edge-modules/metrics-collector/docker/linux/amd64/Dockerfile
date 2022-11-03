# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine

ARG EXE_DIR=.

ENV MODULE_NAME "Microsoft.Azure.Devices.Edge.Azure.Monitor.dll"

RUN apk add --no-cache git curl tini util-linux bash neovim openssl

RUN apk add --no-cache make m4 autoconf automake gcc g++ krb5-dev openssl-dev gettext-dev

RUN apk add --no-cache libtool libxml2 libxslt libunistring-dev zlib-dev samba-dev



RUN git clone https://github.com/gssapi/gss-ntlmssp

WORKDIR gss-ntlmssp

RUN autoreconf -f -i

RUN ./configure --without-manpages --disable-nls

RUN make

RUN make install

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Azure.Monitor.dll
