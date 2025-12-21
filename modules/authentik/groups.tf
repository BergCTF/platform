locals {
  discord_role_mappings = try(
    var.bootstrap.authentication.discord.role_mappings,
    {}
  )

  groups = {
    for name, group in var.bootstrap.groups :
    name => {
      is_superuser    = group.is_superuser
      discord_role_id = lookup(local.discord_role_mappings, name, null)
    }
  }
}

resource "authentik_group" "groups" {
  for_each = local.groups

  name         = each.key
  is_superuser = each.value.is_superuser

  attributes = each.value.discord_role_id == null ? null : jsonencode({
    discord_role_id = each.value.discord_role_id
  })
}
