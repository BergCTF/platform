terraform {
  # required for "enabled" blocks
  required_version = ">= 1.11.0"
  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = ">= 3.0.0"
    }
    helm = {
      source  = "hashicorp/helm"
      version = ">= 3.0.0"
    }
  }
}

# deploy key for infra repository
resource "tls_private_key" "deploy_key" {
  algorithm = "RSA"
  rsa_bits  = 4096
}

# deploy key for challenge repository
resource "tls_private_key" "challenge_deploy_key" {
  algorithm = "RSA"
  rsa_bits  = 4096
}
