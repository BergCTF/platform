variable "generate_deploy_keys" {
  type = list(object({
    repository = string
    title      = string
    read_only  = optional(bool, true)
  }))
  default = []
}
