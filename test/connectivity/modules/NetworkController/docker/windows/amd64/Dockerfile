ARG base_tag=3.1.19-nanoserver-1809
FROM mcr.microsoft.com/dotnet/runtime:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "NetworkController.dll"]
