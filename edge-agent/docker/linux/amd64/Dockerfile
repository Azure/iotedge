FROM microsoft/dotnet:2.0.5-runtime

ARG EXE_DIR=.

# Install snappy and set up symlinks that are absent from the base image
# Required by RocksDb
RUN apt-get update && \
	apt-get install -y libsnappy1v5 && \
	ln -s /lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so && \
	ln -s /lib/x86_64-linux-gnu/libc.so.6 /usr/lib/x86_64-linux-gnu/libc.so

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["/usr/bin/dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
