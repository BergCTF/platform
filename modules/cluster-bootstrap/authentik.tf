# core authentik setup
resource "kubernetes_secret_v1" "authentik_secret_key" {
  lifecycle { enabled = var.authentik.enabled }
  metadata {
    name      = "authentik-extra-secret"
    namespace = kubernetes_namespace_v1.infra_authentik.metadata[0].name
  }
  data = {
    "secret_key"     = random_password.authentik_secret_key.result
    "email_username" = var.email_username
    "email_password" = var.email_password
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "authentik_database_backup_secret" {
  lifecycle { enabled = var.authentik.enabled && var.authentik.backups.enabled }
  metadata {
    name      = "database-authentik-cluster-backup-s3-creds"
    namespace = kubernetes_namespace_v1.infra_authentik.metadata[0].name
  }
  data = {
    "ACCESS_KEY_ID"     = var.authentik.backups.access_key
    "ACCESS_SECRET_KEY" = var.authentik.backups.secret_key
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "authentik_bootstrap_token" {
  lifecycle { enabled = var.authentik.enabled }
  metadata {
    name      = "authentik-bootstrap-token"
    namespace = kubernetes_namespace_v1.infra_authentik.metadata[0].name
  }
  data = {
    "token" = random_password.authentik_bootstrap_token.result
  }
  type = "Opaque"
}

# authentik clients
resource "kubernetes_secret_v1" "argocd_authentik_secret" {
  lifecycle { enabled = var.authentik.enabled }
  metadata {
    name      = "argocd-authentik-secret"
    namespace = kubernetes_namespace_v1.infra_argocd.metadata[0].name
    labels = {
      "app.kubernetes.io/part-of" = "argocd"
    }
  }
  data = {
    "oidc.authentik.clientSecret" = random_password.argocd_client_secret.result
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "grafana_oauth_secret" {
  lifecycle { enabled = var.authentik.enabled && var.monitoring.enabled }
  metadata {
    name      = "auth-generic-oauth-secret"
    namespace = kubernetes_namespace_v1.infra_monitoring.metadata[0].name
  }
  data = {
    "client_id"     = "grafana"
    "client_secret" = random_password.grafana_client_secret.result
  }
  type = "Opaque"
}

resource "kubernetes_secret_v1" "berg_client_secret" {
  lifecycle { enabled = var.authentik.enabled && var.berg.enabled }
  metadata {
    name      = "berg-client-secret"
    namespace = kubernetes_namespace_v1.berg.metadata[0].name
  }
  data = {
    "clientSecret" = random_password.berg_client_secret.result
  }
  type = "Opaque"
}
