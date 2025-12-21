# cross module variables
variable "env" {
  type        = string
  description = "Environment name, eg test"
}

variable "governance_name" {
  type        = string
  description = "Logical grouping of multiple environments"
}

variable "hetzner_token" {
  type        = string
  description = "Hetzner API token for cluster provisioning"
}

check "hetzner_token_set" {
  assert {
    condition     = var.hetzner_token != null
    error_message = "Ensure hetzner_token is set"
  }
}

variable "control_plane_pools" {
  type = list(object({
    name     = string
    type     = string
    location = string
    count    = number
  }))
}

variable "worker_pools" {
  type = list(object({
    name     = string
    type     = string
    location = string
    count    = number
  }))
}

variable "cluster_domain" {
  type = string
}

variable "kube_api_loadbalancer_enabled" {
  type = bool
}

variable "cluster_allow_scheduling_on_control_planes" {
  type = bool
}
