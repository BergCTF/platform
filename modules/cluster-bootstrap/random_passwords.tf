resource "random_password" "argocd_client_secret" {
  length  = 32
  special = false
}

resource "random_password" "grafana_client_secret" {
  length  = 32
  special = false
}

resource "random_password" "authentik_secret_key" {
  length  = 32
  special = false
}

resource "random_password" "berg_client_secret" {
  length  = 32
  special = false
}

resource "random_password" "harbor_client_secret" {
  length  = 32
  special = false
}

resource "random_password" "authentik_bootstrap_token" {
  length  = 32
  special = false
}
