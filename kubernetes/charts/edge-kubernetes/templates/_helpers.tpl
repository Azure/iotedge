{{/* vim: set filetype=mustache: */}}
{{/*
Expand the name of the chart.
*/}}
{{- define "edge-kubernetes.name" -}}
{{- default .Chart.Name | trunc 63 | trimSuffix "-" -}}
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
  {{- if .Values.edgeAgent.env }}
  env:
    {{- if .Values.edgeAgent.env.portMappingServiceType}}
    PortMappingServiceType: {{ .Values.edgeAgent.env.portMappingServiceType | quote }}
    {{- end }}
    {{- if .Values.edgeAgent.env.backupConfigFilePath}}
    BackupConfigFilePath: {{ .Values.edgeAgent.env.backupConfigFilePath | quote }}
    {{- end }}
    {{- if .Values.edgeAgent.env.enableK8sServiceCallTracing}}
    EnableK8sServiceCallTracing: {{ .Values.edgeAgent.env.enableK8sServiceCallTracing | quote }}
    {{- end }}
    {{- if .Values.edgeAgent.env.runtimeLogLevel}}
    RuntimeLogLevel: {{ .Values.edgeAgent.env.runtimeLogLevel | quote }}
    {{- end }}
    {{- if .Values.edgeAgent.env.persistentVolumeClaimDefaultSizeInMb}}
    PersistentVolumeClaimDefaultSizeInMb: {{ .Values.edgeAgent.env.persistentVolumeClaimDefaultSizeInMb | quote }}
    {{- end }}
    {{- if .Values.edgeAgent.env.upstreamProtocol}}
    UpstreamProtocol: {{ .Values.edgeAgent.env.upstreamProtocol | quote }}
    {{- end }}
    {{- if .Values.edgeAgent.env.persistentVolumeName}}
    PersistentVolumeName: {{ .Values.edgeAgent.env.persistentVolumeName | quote }}
    {{- end }}
    {{- if .Values.edgeAgent.env.storageClassName}}
    StorageClassName: {{- if (eq "-" .Values.edgeAgent.env.storageClassName) }} "" {{- else }} {{ .Values.edgeAgent.env.storageClassName | quote }} {{- end }}
    {{- end }}
    {{- if .Values.edgeAgent.env.enableExperimentalFeatures }}
    ExperimentalFeatures__Enabled: {{ .Values.edgeAgent.env.enableExperimentalFeatures | quote }}
    {{- end }}
    {{- if .Values.edgeAgent.env.enableK8sExtensions }}
    ExperimentalFeatures__EnableK8SExtensions: {{ .Values.edgeAgent.env.enableK8sExtensions | quote }}
    {{- end }}
    {{- if .Values.edgeAgent.env.runAsNonRoot }}
    RunAsNonRoot: {{ .Values.edgeAgent.env.runAsNonRoot | quote }}
    {{- end }}
  {{ else }}
  env: {}
  {{ end }}
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
  management_uri: "https://localhost:{{ .Values.iotedged.ports.management }}"
  workload_uri: "https://localhost:{{ .Values.iotedged.ports.workload }}"
listen:
  management_uri: "https://0.0.0.0:{{ .Values.iotedged.ports.management }}"
  workload_uri: "https://0.0.0.0:{{ .Values.iotedged.ports.workload }}"
homedir: {{ .Values.iotedged.data.targetPath | quote }}
namespace: {{ .Release.Namespace | quote }}
device_hub_selector: ""
proxy:
  image: "{{.Values.iotedgedProxy.image.repository}}:{{.Values.iotedgedProxy.image.tag}}"
  image_pull_policy: {{ .Values.iotedgedProxy.image.pullPolicy | quote }}
  {{- if .Values.iotedgedProxy.registryCredentials }}
  auth:
    username: {{ .Values.iotedgedProxy.registryCredentials.username | quote }}
    password: {{ .Values.iotedgedProxy.registryCredentials.password | quote }}
    serveraddress: {{ .Values.iotedgedProxy.registryCredentials.serveraddress | quote }}
  {{ else }}
  auth: {}
  {{ end }}
  config_map_name: "iotedged-proxy-config"
  config_path: "/etc/iotedge-proxy"
  trust_bundle_config_map_name: "iotedged-proxy-trust-bundle"
  trust_bundle_path: "/etc/trust-bundle"
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

