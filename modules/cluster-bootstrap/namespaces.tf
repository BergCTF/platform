resource "kubernetes_namespace_v1" "infra_argocd" {
  metadata {
    name = "infra-argocd"
  }
}

resource "kubernetes_namespace_v1" "infra_monitoring" {
  lifecycle { enabled = var.monitoring.enabled }
  metadata {
    name = "infra-monitoring"
  }
}

resource "kubernetes_namespace_v1" "infra_authentik" {
  lifecycle { enabled = var.authentik.enabled }
  metadata {
    name = "infra-authentik"
  }
}

resource "kubernetes_namespace_v1" "berg" {
  lifecycle { enabled = var.berg.enabled }
  metadata {
    name = "berg"
  }
}

resource "kubernetes_namespace_v1" "infra_logging" {
  lifecycle { enabled = var.monitoring.enabled }
  metadata {
    name = "infra-logging"
  }
}

resource "kubernetes_namespace_v1" "infra_cert_manager" {
  lifecycle { enabled = var.cert_manager.enabled }
  metadata {
    name = "infra-cert-manager"
  }
}

resource "kubernetes_namespace_v1" "infra_external_dns" {
  lifecycle { enabled = var.external_dns.enabled }
  metadata {
    name = "infra-external-dns"
  }
}

resource "kubernetes_namespace_v1" "infra_tracing" {
  lifecycle { enabled = var.monitoring.enabled }
  metadata {
    name = "infra-tracing"
  }
}
