locals {
  map_argocd_webhooks = {
    for o in var.argocd_webhooks :
    o => o
  }
}

resource "random_password" "argocd_webhook_secret" {
  length  = 32
  special = false
}

resource "github_repository_webhook" "argocd_webhook" {
  for_each = local.map_argocd_webhooks

  repository = each.value
  configuration {
    url          = "${var.argocd_url}/api/webhook"
    secret       = random_password.argocd_webhook_secret.result
    content_type = "json"
    insecure_ssl = false
  }

  active = true
  events = ["push"]
}

