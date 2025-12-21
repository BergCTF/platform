resource "kubernetes_secret_v1" "cloudflare_api_token" {
  lifecycle { enabled = var.cert_manager.enabled && var.cert_manager.dns_resolver.enabled }
  metadata {
    name      = "dns-resolver-api-token-secret"
    namespace = kubernetes_namespace_v1.infra_cert_manager.metadata[0].name
  }
  data = {
    "api-token" = var.cert_manager.dns_resolver.api_token
  }
  type = "Opaque"
}
