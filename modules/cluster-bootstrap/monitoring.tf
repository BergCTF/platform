
resource "kubernetes_secret_v1" "loki_objectstorage_credentials" {
  lifecycle { enabled = var.monitoring.enabled && var.monitoring.loki.enabled }
  metadata {
    name      = "loki-objectstorage-credentials"
    namespace = kubernetes_namespace_v1.infra_logging.metadata[0].name
  }
  data = {
    "S3_ACCESS_KEY_ID"     = var.monitoring.loki.access_key
    "S3_SECRET_ACCESS_KEY" = var.monitoring.loki.secret_key
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "mimir_objectstorage_credentials" {
  lifecycle { enabled = var.monitoring.enabled && var.monitoring.mimir.enabled }
  metadata {
    name      = "mimir-objectstorage-credentials"
    namespace = kubernetes_namespace_v1.infra_monitoring.metadata[0].name
  }
  data = {
    "S3_ACCESS_KEY_ID"     = var.monitoring.mimir.access_key
    "S3_SECRET_ACCESS_KEY" = var.monitoring.mimir.secret_key
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "tracing_objectstorage_credentials" {
  lifecycle { enabled = var.monitoring.enabled && var.monitoring.tempo.enabled }
  metadata {
    name      = "tracing-objectstorage-credentials"
    namespace = kubernetes_namespace_v1.infra_tracing.metadata[0].name
  }
  data = {
    "AWS_ACCESS_KEY_ID"     = var.monitoring.tempo.access_key
    "AWS_SECRET_ACCESS_KEY" = var.monitoring.tempo.secret_key
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "discord_alert_webhook" {
  lifecycle { enabled = var.monitoring.enabled && var.monitoring.discord_webhook != null }
  metadata {
    name      = "discord-webhook"
    namespace = kubernetes_namespace_v1.infra_monitoring.metadata[0].name
  }
  data = {
    "webhook" = var.monitoring.discord_webhook
  }
  type = "Opaque"
}
