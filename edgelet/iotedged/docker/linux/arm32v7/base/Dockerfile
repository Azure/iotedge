FROM arm32v7/debian:9-slim

# package installation needs to exec as root
RUN apt-get update && apt-get install -y \
  libssl1.0.2 \
  ca-certificates \
  && rm -rf /var/lib/apt/lists/*

# Add an system user account for running the module
RUN useradd -ms /bin/bash edgeletuser
