ARG base_tag=2.1.0-runtime-nanoserver-1803
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "load-gen.dll"]