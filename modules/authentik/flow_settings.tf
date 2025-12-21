resource "authentik_stage_prompt_field" "prompt_field_settings_username" {
  name                     = "user-settings-field-username"
  field_key                = "username"
  label                    = "Username"
  type                     = "username"
  required                 = true
  placeholder              = "Username"
  initial_value            = <<-EOF
  try:
    return user.username
  except:
    return ""
  EOF
  initial_value_expression = true
  order                    = 200
}

resource "authentik_stage_prompt_field" "prompt_field_settings_email" {
  name                     = "user-settings-field-email"
  field_key                = "email"
  label                    = "Email"
  type                     = "email"
  required                 = true
  placeholder              = "Email"
  initial_value            = <<-EOF
  try:
    return user.email
  except:
    return ""
  EOF
  initial_value_expression = true
  order                    = 202
}

resource "authentik_stage_prompt" "user_settings" {
  name = "user-settings-prompt"
  fields = [
    resource.authentik_stage_prompt_field.prompt_field_settings_username.id,
    resource.authentik_stage_prompt_field.prompt_field_settings_email.id,
  ]
}


resource "authentik_stage_user_write" "settings_user_write" {
  name                     = "settings-user-write"
  create_users_as_inactive = false
  user_creation_mode       = "never_create"
}


resource "authentik_flow" "settings_flow" {
  name           = "settings"
  title          = "Settings"
  slug           = "settings"
  designation    = "stage_configuration"
  authentication = "require_authenticated"
  background     = var.authentik.branding.background_image
}

resource "authentik_flow_stage_binding" "settings_prompt" {
  target                  = authentik_flow.settings_flow.uuid
  stage                   = authentik_stage_prompt.user_settings.id
  evaluate_on_plan        = false
  re_evaluate_policies    = true
  policy_engine_mode      = "any"
  invalid_response_action = "retry"
  order                   = 20
}

resource "authentik_flow_stage_binding" "settings_write" {
  target                  = authentik_flow.settings_flow.uuid
  stage                   = authentik_stage_user_write.settings_user_write.id
  evaluate_on_plan        = false
  re_evaluate_policies    = true
  policy_engine_mode      = "any"
  invalid_response_action = "retry"
  order                   = 100
}

resource "authentik_policy_binding" "settings_access_player" {
  target = authentik_flow.settings_flow.uuid
  group  = authentik_group.groups[var.bootstrap.default_group].id
  order  = 0
}
