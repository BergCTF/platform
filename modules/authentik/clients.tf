

locals {
  role_scope_mappings = {
    for mapping in flatten([
      for _, client in var.bootstrap.oidc_clients :
      values(try(client.extra_scope_mappings, {}))
    ]) :
    mapping.scope_name => mapping
  }
  app_group_bindings = flatten([
    for app_key, client in var.bootstrap.oidc_clients : [
      for group_name in try(client.limit_to_groups, []) : {
        app_key    = app_key
        group_name = group_name
      }
    ]
  ])
}

resource "authentik_property_mapping_provider_scope" "role_scope_mapping" {
  for_each = local.role_scope_mappings

  name       = each.key
  scope_name = each.value.scope_name
  expression = each.value.expression
}

resource "authentik_provider_oauth2" "oidc_provider" {
  for_each      = var.bootstrap.oidc_clients
  name          = each.value.slug
  client_id     = each.value.client_id
  client_secret = try(each.value.client_secret, null)

  allowed_redirect_uris = [
    for uri in each.value.redirect_uris : {
      matching_mode = "strict"
      url           = uri
    }
  ]
  sub_mode = "user_uuid"
  property_mappings = concat(
    [
      for name, mapping in try(each.value.extra_scope_mappings, {}) :
      authentik_property_mapping_provider_scope.role_scope_mapping[mapping.scope_name].id
    ],
    [
      data.authentik_property_mapping_provider_scope.openid_email.id,
      data.authentik_property_mapping_provider_scope.openid_openid.id,
      data.authentik_property_mapping_provider_scope.openid_profile.id,
    ]
  )
  authentication_flow = authentik_flow.berg_authentication_flow.uuid
  authorization_flow  = data.authentik_flow.default_authorization_flow.id
  invalidation_flow   = data.authentik_flow.default_invalidation_flow.id
  signing_key         = data.authentik_certificate_key_pair.generated.id
}

resource "authentik_application" "oidc_app" {
  for_each          = var.bootstrap.oidc_clients
  name              = each.value.name
  meta_launch_url   = try(each.value.launch_url, null)
  slug              = each.value.slug
  protocol_provider = resource.authentik_provider_oauth2.oidc_provider[each.key].id
}

resource "authentik_policy_binding" "group_access" {
  for_each = {
    for idx, binding in local.app_group_bindings :
    "${binding.app_key}:${binding.group_name}" => binding
  }

  target = authentik_application.oidc_app[each.value.app_key].uuid
  group  = authentik_group.groups[each.value.group_name].id
  order  = 0
}
