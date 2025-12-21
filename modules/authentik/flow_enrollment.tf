resource "authentik_flow" "berg_enrollment_flow" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  name           = "berg-enrollment"
  title          = "Register User"
  slug           = "berg-enrollment"
  designation    = "enrollment"
  authentication = "require_unauthenticated"
  background     = var.authentik.branding.background_image
}

resource "authentik_stage_prompt_field" "prompt_field_password" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  name                   = "prompt-field-password"
  field_key              = "password"
  label                  = "Password"
  type                   = "password"
  required               = true
  placeholder            = "Password"
  placeholder_expression = false
  order                  = 3
}


resource "authentik_stage_prompt_field" "prompt_field_password_repeat" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  name                   = "prompt-field-password-repeat"
  field_key              = "password_repeat"
  label                  = "Password (repeat)"
  type                   = "password"
  required               = true
  placeholder            = "Password (repeat)"
  placeholder_expression = false
  order                  = 4
}

resource "authentik_stage_prompt_field" "prompt_field_username" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  name                   = "prompt-field-username"
  field_key              = "username"
  label                  = "Username"
  type                   = "username"
  required               = true
  placeholder            = "Username"
  placeholder_expression = false
  order                  = 0
}

resource "authentik_stage_prompt" "prompt_first" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  name = "berg-enrollment-prompt-first"
  fields = [
    resource.authentik_stage_prompt_field.prompt_field_username.id,
    resource.authentik_stage_prompt_field.prompt_field_email.id,
    resource.authentik_stage_prompt_field.prompt_field_password.id,
    resource.authentik_stage_prompt_field.prompt_field_password_repeat.id,
  ]
}

resource "authentik_stage_prompt_field" "prompt_field_email" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  name                   = "prompt-field-email"
  field_key              = "email"
  label                  = "Email"
  type                   = "email"
  required               = true
  placeholder            = "Email"
  placeholder_expression = false
  order                  = 1
}

resource "authentik_stage_captcha" "cloudflare_captcha" {
  lifecycle {
    enabled = var.bootstrap.captcha.enabled
  }
  name        = "cloudflare-captcha"
  private_key = var.bootstrap.captcha.private_key
  public_key  = var.bootstrap.captcha.public_key
  interactive = true
  api_url     = var.bootstrap.captcha.api_url
  js_url      = var.bootstrap.captcha.js_url
}

resource "authentik_stage_user_write" "register_user_write" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  name                     = "user-write"
  create_users_as_inactive = true
  user_creation_mode       = "always_create"
  user_type                = "internal"
  create_users_group       = authentik_group.groups[var.bootstrap.default_group].id
}

resource "authentik_stage_email" "register_email_verification" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  name                     = "user-email-verification"
  template                 = "confirm.html"
  subject                  = "[${var.authentik.branding.title}] Confirm your email"
  activate_user_on_success = true
  use_global_settings      = true
}

resource "authentik_flow_stage_binding" "register_prompt_first" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  target = authentik_flow.berg_enrollment_flow.uuid
  stage  = authentik_stage_prompt.prompt_first.id
  order  = 10
}

resource "authentik_flow_stage_binding" "register_captcha" {
  lifecycle {
    enabled = var.bootstrap.captcha.enabled
  }
  target = authentik_flow.berg_enrollment_flow.uuid
  stage  = authentik_stage_captcha.cloudflare_captcha.id
  order  = 12
}

resource "authentik_flow_stage_binding" "register_user_write" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  target = authentik_flow.berg_enrollment_flow.uuid
  stage  = authentik_stage_user_write.register_user_write.id
  order  = 13
}

resource "authentik_flow_stage_binding" "register_email_verification" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  target = authentik_flow.berg_enrollment_flow.uuid
  stage  = authentik_stage_email.register_email_verification.id
  order  = 14
}

resource "authentik_flow_stage_binding" "register_user_login" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  target = authentik_flow.berg_enrollment_flow.uuid
  stage  = authentik_stage_user_login.user_login.id
  order  = 15
}
