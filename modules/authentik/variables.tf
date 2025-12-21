variable "authentik_token" {
  type = string
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
  })
}
variable "bootstrap" {
  type = object({
    default_group = string
    groups = optional(map(object({
      is_superuser = optional(bool, false)
    })), {})
    oidc_clients = optional(map(object({
      name            = string
      client_id       = string
      client_secret   = optional(string)
      redirect_uris   = list(string)
      limit_to_groups = optional(list(string), [])
      launch_url      = optional(string, "")
      slug            = string
      extra_scope_mappings = optional(map(object({
        scope_name = string
        expression = string
      })), {})
    })), {})
    authentication = optional(object({
      # enable discord federated authentication
      discord = optional(object({
        # discord is currently required
        enabled = optional(bool, true)
        # required for role mappings
        guild_id = optional(string)
        # arbitrary group -> discord role id mappings
        role_mappings = optional(map(string), {})
        client_id     = optional(string)
        client_secret = optional(string)
      }), {})
      # enable password authentication
      password = optional(object({
        enabled = optional(bool, false)
      }), {})
    }), {})
    # smtp configuration is required if password authentication is enabled
    smtp = optional(object({
      enabled = optional(bool, false)
    }), {})
    # cloudflare turnstile captcha
    captcha = optional(object({
      enabled     = optional(bool, false)
      private_key = optional(string, "")
      public_key  = optional(string, "")
      api_url     = optional(string, "")
      js_url      = optional(string, "")
    }), {})
  })
}

