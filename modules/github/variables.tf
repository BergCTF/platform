variable "generate_deploy_keys" {
  type = list(object({
    repository = string
    title      = string
    read_only  = optional(bool, true)
  }))
  default = []
}

variable "argocd_webhooks" {
  type    = list(string)
  default = []
}

variable "argocd_url" {
  type        = string
  description = "Base URL to the argocd server"
}
