resource "kubernetes_namespace_v1" "infra_argocd" {
  metadata {
    name = "infra-argocd"
    labels = {
      "app.kubernetes.io/name"      = "infra-argocd"
      "app.kubernetes.io/component" = "infra"
      "app.kubernetes.io/part-of"   = "infra"
    }
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
    labels = {
      "app.kubernetes.io/component" = "infra"
      "app.kubernetes.io/name"      = "infra-authentik"
      "app.kubernetes.io/part-of"   = "infra"
    }
  }
}

resource "kubernetes_namespace_v1" "berg" {
  lifecycle { enabled = var.berg.enabled }
  metadata {
    name = "berg"
    labels = {
      "app.kubernetes.io/name"      = "berg"
      "app.kubernetes.io/component" = "berg"
      "app.kubernetes.io/part-of"   = "berg"
    }
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
    labels = {
      "app.kubernetes.io/name"      = "infra-cert-manager"
      "app.kubernetes.io/component" = "infra"
      "app.kubernetes.io/part-of"   = "infra"
    }
  }
}

resource "kubernetes_namespace_v1" "infra_external_dns" {
  lifecycle { enabled = var.external_dns.enabled }
  metadata {
    name = "infra-external-dns"
    labels = {
      "app.kubernetes.io/name"      = "infra-external-dns"
      "app.kubernetes.io/component" = "infra"
      "app.kubernetes.io/part-of"   = "infra"
    }
  }
}

resource "kubernetes_namespace_v1" "infra_tracing" {
  lifecycle { enabled = var.monitoring.enabled }
  metadata {
    name = "infra-tracing"
  }
}
