resource "kubernetes_secret_v1" "berg_database_backup_secret" {
  lifecycle { enabled = var.berg.enabled && var.berg.backups.enabled }
  metadata {
    name      = "database-berg-cluster-backup-s3-creds"
    namespace = kubernetes_namespace_v1.berg.metadata[0].name
  }
  data = {
    "ACCESS_KEY_ID"     = var.berg.backups.access_key
    "ACCESS_SECRET_KEY" = var.berg.backups.secret_key
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "berg_pull_secret" {
  lifecycle { enabled = var.berg.enabled }
  metadata {
    name      = "berg-pull-secret"
    namespace = kubernetes_namespace_v1.berg.metadata[0].name
  }
  binary_data = {
    ".dockerconfigjson" = var.berg.pull_secret
  }
  type = "kubernetes.io/dockerconfigjson"
}

resource "kubernetes_secret_v1" "berg_discord_client" {
  lifecycle { enabled = var.berg.enabled }
  metadata {
    name      = "berg-discord-client"
    namespace = kubernetes_namespace_v1.berg.metadata[0].name
  }
  data = {
    "clientId"     = var.discord_client_id
    "clientSecret" = var.discord_client_secret
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "berg_discord_bot_token" {
  lifecycle { enabled = var.berg.enabled }
  metadata {
    name      = "berg-discord-bot-token"
    namespace = kubernetes_namespace_v1.berg.metadata[0].name
  }
  data = {
    "token" = var.discord_bot_token
  }
  type = "Opaque"
}
