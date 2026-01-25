# cross module variables
variable "env" {
  type        = string
  description = "Environment name, eg test"
}

variable "governance_name" {
  type        = string
  description = "Logical grouping of multiple environments"
}

# variables for cluster module
variable "hetzner_token" {
  type        = string
  description = "API Token for Hetzner infrastructure"
}

# cluster configuration
variable "cluster" {
  type = object({
    domain                                     = string
    kube_api_loadbalancer_enabled              = optional(bool, false)
    cluster_allow_scheduling_on_control_planes = optional(bool, false)

    control_plane_pools = list(object({
      name     = string
      type     = string
      location = string
      count    = number
    }))

    worker_pools = list(object({
      name     = string
      type     = string
      location = string
      count    = number
    }))

    infra_repo = object({
      url  = string
      path = string
    })
  })
}

variable "berg" {
  type = object({
    enabled           = bool
    domain            = optional(string)
    challenge_repo    = optional(string)
    pull_secret       = optional(string)
    admin_access_only = optional(bool, true)
    redirect_uris     = optional(list(string), [])
    backups = object({
      enabled    = optional(bool, false)
      access_key = optional(string)
      secret_key = optional(string)
    })
    discord_bot_token = optional(string, null)
  })
}

variable "harbor" {
  type = object({
    enabled    = optional(bool, false)
    domain     = optional(string)
    access_key = optional(string)
    secret_key = optional(string)
  })
}

variable "authentik" {
  type = object({
    enabled = bool
    domain  = optional(string)
    branding = object({
      title               = string
      favicon             = string
      logo                = string
      background_image    = string
      default_application = string
    })
    backups = object({
      enabled    = optional(bool, false)
      access_key = optional(string)
      secret_key = optional(string)
    })
    authentication = optional(object({
      # enable discord federated authentication
      discord = optional(object({
        # discord is currently required
        enabled = optional(bool, true)
        # required for role mappings
        guild_id      = optional(string)
        client_id     = optional(string)
        client_secret = optional(string)
        # arbitrary group -> discord role id mappings
        role_mappings = optional(map(string), {})
      }), {})
      # enable password authentication
      password = optional(object({
        enabled = optional(bool, false)
      }), {})
    }), {})
    # smtp configuration is required if password authentication is enabled
    smtp = optional(object({
      enabled  = optional(bool, false)
      username = optional(string, null)
      password = optional(string, null)
    }), {})
    # cloudflare turnstile captcha
    captcha = optional(object({
      enabled = optional(bool, false)
    }), {})
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

variable "discord_client_id" {
  type     = string
  default  = null
  nullable = true
}
variable "discord_client_secret" {
  type     = string
  default  = null
  nullable = true
}

variable "dns_api_token" {
  type     = string
  default  = null
  nullable = true
}

variable "cert_manager" {
  type = object({
    enabled = optional(bool, false)
    dns_resolver = object({
      enabled = optional(bool, false)
    })
  })
}

variable "external_dns" {
  type = object({
    enabled = optional(bool, true)
  })
}

