FROM grafana/grafana:6.5.1

COPY ./config/grafana.ini /etc/grafana/grafana.ini
COPY ./config/datasources.yaml /etc/grafana/provisioning/datasources
COPY ./config/dashboard.yaml /etc/grafana/provisioning/dashboards

# Follow this pattern to add your own created dashboards
COPY  ./dashboards/ /etc/grafana/provisioning/dashboards/

USER root

RUN chown -R grafana:grafana /etc/grafana/provisioning
USER grafana
