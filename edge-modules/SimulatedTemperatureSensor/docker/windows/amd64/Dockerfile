ARG base_tag=2.1.6-runtime-nanoserver-1809
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN net user /add moduleuser

USER moduleuser
CMD ["dotnet", "SimulatedTemperatureSensor.dll"]