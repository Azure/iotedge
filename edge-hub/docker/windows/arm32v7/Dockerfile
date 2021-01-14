ARG base_tag=1.0.6-windows-arm32v7
FROM azureiotedge/azureiotedge-hub-base:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

# Expose MQTT, AMQP and HTTPS ports
EXPOSE 8883/tcp
EXPOSE 5671/tcp
EXPOSE 443/tcp

ENV OptimizeForPerformance false
ENV MqttEventsProcessorThreadCount 1
CMD ["dotnet", "Microsoft.Azure.Devices.Edge.Hub.Service.dll"]
