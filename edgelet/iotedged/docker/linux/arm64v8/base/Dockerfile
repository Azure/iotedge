FROM arm64v8/debian:9-slim

RUN apt-get update && apt-get install -y \
  libssl1.0.2 \
  ca-certificates \
  && rm -rf /var/lib/apt/lists/*
