resource "helm_release" "infra_argocd" {
  name       = "argocd"
  repository = "https://argoproj.github.io/argo-helm"
  chart      = "argo-cd"
  namespace  = kubernetes_namespace_v1.infra_argocd.metadata[0].name
  version    = "9.3.4"

  # install crds via talos
  skip_crds = true
  set = [
    {
      name  = "crds.install"
      value = "false"
    }
  ]
}

# required appprojects for initial sync wave
resource "kubernetes_manifest" "appproject_argocd_apps" {
  depends_on = [helm_release.infra_argocd]
  manifest   = yamldecode(file("../manifests/${var.governance_name}-${var.env}/appprojects/argocd-apps.yaml"))
}

resource "kubernetes_manifest" "appproject_misc_argocd_apps" {
  depends_on = [helm_release.infra_argocd]
  manifest   = yamldecode(file("../manifests/${var.governance_name}-${var.env}/appprojects/misc-argocd-apps.yaml"))
}

resource "kubernetes_manifest" "appproject_appprojects" {
  depends_on = [helm_release.infra_argocd]
  manifest   = yamldecode(file("../manifests/${var.governance_name}-${var.env}/appprojects/appprojects.yaml"))
}

# misc apps - self-referential
resource "kubernetes_manifest" "app_misc_apps" {
  depends_on = [helm_release.infra_argocd]
  manifest   = yamldecode(file("../manifests/${var.governance_name}-${var.env}/misc-apps.yaml"))
}

resource "kubernetes_secret_v1" "argocd_infra_repo" {
  metadata {
    name      = "repo-argocd-configs"
    namespace = kubernetes_namespace_v1.infra_argocd.metadata[0].name
    labels = {
      "argocd.argoproj.io/secret-type" = "repository"
    }
  }

  data = {
    type          = "git"
    url           = var.infra_repo
    sshPrivateKey = var.infra_repo_deploy_key
  }

  type = "Opaque"
}

resource "kubernetes_secret_v1" "argocd_challenge_repo" {
  lifecycle { enabled = var.berg.enabled }
  metadata {
    name      = "repo-challenges"
    namespace = kubernetes_namespace_v1.infra_argocd.metadata[0].name
    labels = {
      "argocd.argoproj.io/secret-type" = "repository"
    }
  }

  data = {
    type          = "git"
    url           = var.berg.challenge_repo
    sshPrivateKey = var.berg.challenge_repo_deploy_key
  }

  type = "Opaque"
}
