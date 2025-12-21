# cross module variables
variable "env" {
  type        = string
  description = "Environment name, eg test"
}

variable "governance_name" {
  type        = string
  description = "Logical grouping of multiple environments"
}

# argocd infra repo settings
variable "infra_repo" {
  type        = string
  description = "url of argocd infra repo"
}

variable "infra_repo_deploy_key" {
  type        = string
  description = "infra repo deploy key"
}

variable "infra_repo_path" {
  type        = string
  description = "path in argocd infra repo"
}

# email is required for password login -otherwise only discord is supported
variable "email_enabled" {
  type    = bool
  default = false
}

variable "email_username" {
  type    = string
  default = null
}

variable "email_password" {
  type    = string
  default = null
}

check "email_username_set" {
  assert {
    condition     = !var.email_enabled || var.email_username != null
    error_message = "Ensure email_username is set"
  }
}

check "email_password_set" {
  assert {
    condition     = !var.email_enabled || var.email_password != null
    error_message = "Ensure email_password is set"
  }
}

variable "berg" {
  type = object({
    enabled                   = bool
    pull_secret               = optional(string)
    challenge_repo            = optional(string)
    challenge_repo_deploy_key = optional(string)
    backups = object({
      enabled    = optional(bool, false)
      access_key = optional(string)
      secret_key = optional(string)
    })
  })
}

check "challenge_repo_set" {
  assert {
    condition     = !var.berg.enabled || var.berg.challenge_repo != null
    error_message = "Ensure challenge_repo is set"
  }
}

check "challenge_repo_deploy_key_set" {
  assert {
    condition     = !var.berg.enabled || var.berg.challenge_repo_deploy_key != null
    error_message = "Ensure challenge_repo_deploy_key is set"
  }
}

check "pull_secret_set" {
  assert {
    condition     = !var.berg.enabled || var.berg.pull_secret != null
    error_message = "Ensure pull_secret is set"
  }
}

variable "authentik" {
  type = object({
    enabled = bool
    backups = object({
      enabled    = optional(bool, false)
      access_key = optional(string)
      secret_key = optional(string)
    })
  })
}

variable "monitoring" {
  type = object({
    enabled         = optional(bool, false)
    discord_webhook = optional(string)
    loki = object({
      enabled    = optional(bool, false)
      access_key = optional(string)
      secret_key = optional(string)
    })
    mimir = object({
      enabled    = optional(bool, false)
      access_key = optional(string)
      secret_key = optional(string)
    })
    tempo = object({
      enabled    = optional(bool, false)
      access_key = optional(string)
      secret_key = optional(string)
    })
  })
}

variable "cert_manager" {
  type = object({
    enabled = optional(bool, true)
    dns_resolver = object({
      enabled   = optional(bool, false)
      api_token = optional(string)
    })
  })
}

check "dns_resolver_api_token_set" {
  assert {
    condition     = !var.cert_manager.enabled || !var.cert_manager.dns_resolver.enabled || var.cert_manager.dns_resolver.api_token != null
    error_message = "Ensure dns_resolver_api_token is set"
  }
}
