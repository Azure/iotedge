FROM mcr.microsoft.com/windows:1809-amd64 AS builder

RUN powershell.exe -Command Invoke-WebRequest "https://aka.ms/vs/16/release/VC_redist.x64.exe" -OutFile C:\VC_redist.x64.exe
RUN C:\VC_redist.x64.exe /install /quiet /norestart

FROM mcr.microsoft.com/windows/nanoserver:1809-amd64

ARG EXE_DIR=.

WORKDIR /app

COPY --from=builder /Windows/System32/vcruntime140.dll /Windows/System32/
COPY $EXE_DIR/snitcher.exe ./

CMD ["snitcher.exe"]
