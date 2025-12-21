terraform {
  # required for "enabled" blocks
  required_version = ">= 1.11.0"
  required_providers {
    openbao = {
      source  = "hashicorp/vault"
      version = ">= 5.6.0"
    }
  }
}
