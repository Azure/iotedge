ARG base_tag=2.1.13-bionic-arm32v7
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser