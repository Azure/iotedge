FROM mcr.microsoft.com/windows/nanoserver:1809-amd64

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["snitcher.exe"]