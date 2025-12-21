locals {
  map_generate_deploy_keys = {
    for o in var.generate_deploy_keys :
    "${o.repository}-${o.title}" => o
  }
}

resource "tls_private_key" "generated_deploy_key" {
  for_each  = local.map_generate_deploy_keys
  algorithm = "ED25519"
}

resource "github_repository_deploy_key" "generated_deploy_key" {
  for_each   = local.map_generate_deploy_keys
  title      = each.value.title
  repository = each.value.repository
  key        = tls_private_key.generated_deploy_key[each.key].public_key_openssh
  read_only  = each.value.read_only
}

