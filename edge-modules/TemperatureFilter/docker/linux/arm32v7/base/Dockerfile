FROM microsoft/dotnet:2.0.6-runtime-stretch-arm32v7

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser