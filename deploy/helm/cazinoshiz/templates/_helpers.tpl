{{- define "cazinoshiz.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "cazinoshiz.labels" -}}
app.kubernetes.io/name: {{ include "cazinoshiz.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{- define "cazinoshiz.secret" -}}
{{- .Values.secrets.name -}}
{{- end -}}
