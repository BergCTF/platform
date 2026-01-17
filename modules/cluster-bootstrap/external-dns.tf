resource "kubernetes_secret_v1" "ed_dns_api_token" {
  lifecycle { enabled = var.external_dns.enabled }
  metadata {
    name      = "dns-api-key"
    namespace = kubernetes_namespace_v1.infra_external_dns.metadata[0].name
  }
  data = {
    "api-token" = var.dns_api_token
  }
  type = "Opaque"
}
