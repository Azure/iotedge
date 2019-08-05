{{/* vim: set filetype=mustache: */}}
{{/*
Expand the name of the chart.
*/}}
{{- define "edge-kubernetes.name" -}}
{{- default .Chart.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "edge-kubernetes.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "edge-kubernetes.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/* Template for iotedged's configuration YAML. */}}
{{- define "edge-kubernetes.iotedgedconfig" }}
provisioning:
  source: "manual"
  device_connection_string: {{ .Values.deviceConnectionString | quote }}
{{- if .Values.iotedged.certificates }}
certificates:
  device_ca_cert: "/etc/edgecerts/device_ca_cert"
  device_ca_pk: "/etc/edgecerts/device_ca_pk"
  trusted_ca_certs: "/etc/edgecerts/trusted_ca_certs"
{{ end }}
agent:
  name: "edgeAgent"
  type: "docker"
  env: {}
  config:
    image: "{{ .Values.edgeAgent.image.repository }}:{{ .Values.edgeAgent.image.tag }}"
  {{- if .Values.edgeAgent.registryCredentials }}
    auth:
      username: {{ .Values.edgeAgent.registryCredentials.username | quote }}
      password: {{ .Values.edgeAgent.registryCredentials.password | quote }}
      serveraddress: {{ .Values.edgeAgent.registryCredentials.serveraddress | quote }}
    {{ else }}
    auth: {}
    {{ end }}      
hostname: {{ .Values.edgeAgent.hostname }}
connect:
  management_uri: "http://localhost:{{ .Values.iotedged.ports.management }}"
  workload_uri: "http://localhost:{{ .Values.iotedged.ports.workload }}"
listen:
  management_uri: "http://0.0.0.0:{{ .Values.iotedged.ports.management }}"
  workload_uri: "http://0.0.0.0:{{ .Values.iotedged.ports.workload }}"
homedir: {{ .Values.iotedged.data.targetPath | quote }}
namespace: {{ .Release.Namespace | quote }}
use_pvc: False
proxy_image:  "{{.Values.iotedgedProxy.image.repository}}:{{.Values.iotedgedProxy.image.tag}}"
proxy_config_path: "/etc/traefik"
proxy_config_map_name: "iotedged-proxy-config"
image_pull_policy: {{ .Values.iotedgedProxy.image.pullPolicy | quote }}
service_account_name: "iotedge"
device_hub_selector: ""
{{ end }}

{{/* Template for rendering registry credentials. */}}
{{- define "edge-kubernetes.regcreds" }}
auths:
  {{- range $key, $val := .Values.registryCredentials }}
  {{ $key | quote }}:
    auth: {{ printf "%s:%s" $val.username $val.password | b64enc | quote }}
  {{- end }}
{{- end }}

{{/*
Parse the device ID from connection string.
*/}}
{{- define "edge-kubernetes.deviceid" -}}
{{- regexFind "DeviceId=[^;]+" .Values.deviceConnectionString | regexFind "=.+" | substr 1 -1 | lower -}}
{{- end -}}

{{/*
Parse the host name from connection string.
*/}}
{{- define "edge-kubernetes.hostname" -}}
{{- regexFind "HostName=[^;]+" .Values.deviceConnectionString | regexFind "=.+" | substr 1 -1 | lower -}}
{{- end -}}

{{/*
Parse the hub name from connection string.
*/}}
{{- define "edge-kubernetes.hubname" -}}
{{- include "edge-kubernetes.hostname" . | splitList "." | first | lower -}}
{{- end -}}

{{/*
Generate namespace from release namespace parameter.
*/}}
{{- define "edge-kubernetes.namespace" -}}
{{ .Release.Namespace }}
{{- end -}}

