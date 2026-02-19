module "talos" {
  source  = "hcloud-k8s/kubernetes/hcloud"
  version = "3.22.0"

  cluster_name = "${var.env}-${var.governance_name}"
  hcloud_token = var.hetzner_token

  cluster_delete_protection = false

  # Export configs for Talos and Kube API access
  # cluster_kubeconfig_path  = "${var.env}.kubeconfig"
  # cluster_talosconfig_path = "${var.env}.talosconfig"

  # Optional Ingress Controller and Cert Manager
  longhorn_enabled                              = false
  cert_manager_enabled                          = false
  ingress_nginx_enabled                         = false
  kube_api_load_balancer_enabled                = var.kube_api_loadbalancer_enabled
  kube_api_load_balancer_public_network_enabled = true
  cluster_allow_scheduling_on_control_planes    = var.cluster_allow_scheduling_on_control_planes
  kube_api_extra_args = {
    # TODO: there's more consistent ways to derive this
    oidc-issuer-url     = "https://idp.${var.cluster_domain}/application/o/kubernetes-api/"
    oidc-client-id      = "kubernetes-api"
    oidc-username-claim = "email"
    oidc-groups-claim   = "groups"
    oidc-groups-prefix  = "oidc:"
  }

  talos_extra_remote_manifests = [
    # Add CRDs for ArgoCD
    "https://raw.githubusercontent.com/argoproj/argo-cd/refs/heads/master/manifests/crds/application-crd.yaml",
    "https://raw.githubusercontent.com/argoproj/argo-cd/refs/heads/master/manifests/crds/appproject-crd.yaml",
    "https://raw.githubusercontent.com/argoproj/argo-cd/refs/heads/master/manifests/crds/applicationset-crd.yaml",
    # Add CRDs for Gateway API
    "https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.4.1/experimental-install.yaml",
  ]

  # Allow access from anywhere.
  firewall_api_source = [
    "0.0.0.0/0"
  ]

  # since berg currently requires NodePort services for publicPort exposure, we need to expose the nodeport range to the internet :(
  # 30000-32767
  firewall_extra_rules = [
    {
      description = "node port tcp access for all nodes"
      direction   = "in"
      source_ips  = ["0.0.0.0/0"]
      protocol    = "tcp"
      port        = "30000-32767"
    },
    {
      description = "node port udp access for all nodes"
      direction   = "in"
      source_ips  = ["0.0.0.0/0"]
      protocol    = "udp"
      port        = "30000-32767"
    }
  ]

  # we use kgateway
  cilium_helm_values = {
    ingressController = {
      enabled = false
    }
  }

  control_plane_nodepools = var.control_plane_pools
  worker_nodepools        = var.worker_pools

}
