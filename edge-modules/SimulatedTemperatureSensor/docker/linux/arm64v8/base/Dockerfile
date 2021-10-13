ARG base_tag=3.1.18-bionic-arm64v8
FROM mcr.microsoft.com/dotnet/runtime:${base_tag}

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser
