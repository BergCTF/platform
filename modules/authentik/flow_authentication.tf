resource "authentik_flow" "berg_authentication_flow" {
  name           = "berg-authentication"
  title          = "Login"
  slug           = "berg-authentication"
  designation    = "authentication"
  authentication = "require_unauthenticated"
  background     = var.authentik.branding.background_image
}

resource "authentik_policy_expression" "berg_authentication_password_stage" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  name       = "berg-authentication-password-stage"
  expression = <<-EOF
    flow_plan = request.context.get("flow_plan")
    if not flow_plan:
      return True
    return not hasattr(flow_plan.context.get("pending_user"), "backend")
  EOF
}

resource "authentik_stage_password" "berg_authentication_password" {
  name = "berg-authentication-password"
  backends = [
    "authentik.core.auth.InbuiltBackend",
  ]
}

resource "authentik_stage_identification" "user_identification" {
  name                      = "berg-authentication-identification"
  case_insensitive_matching = true
  pretend_user_exists       = true
  show_matched_user         = true
  enrollment_flow           = var.bootstrap.authentication.password.enabled ? authentik_flow.berg_enrollment_flow.uuid : null
  recovery_flow             = var.bootstrap.authentication.password.enabled ? authentik_flow.berg_recovery_flow.uuid : null
  captcha_stage             = authentik_stage_captcha.cloudflare_captcha.id
  user_fields = var.bootstrap.authentication.password.enabled ? [
    "username",
    "email"
  ] : []
  sources = var.bootstrap.authentication.discord.enabled ? [
    authentik_source_oauth.discord.uuid
  ] : []
}

resource "authentik_flow_stage_binding" "authenticate_identification" {
  target = authentik_flow.berg_authentication_flow.uuid
  stage  = authentik_stage_identification.user_identification.id
  order  = 10
}

resource "authentik_flow_stage_binding" "authenticate_password" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  target = authentik_flow.berg_authentication_flow.uuid
  stage  = authentik_stage_password.berg_authentication_password.id
  order  = 20
}

resource "authentik_flow_stage_binding" "authenticate_user_login" {
  target = authentik_flow.berg_authentication_flow.uuid
  stage  = authentik_stage_user_login.user_login.id
  order  = 30
}

resource "authentik_policy_binding" "authenticate_password_stage" {
  lifecycle { enabled = var.bootstrap.authentication.password.enabled }
  order  = 100
  target = authentik_flow_stage_binding.authenticate_password.id
  policy = authentik_policy_expression.berg_authentication_password_stage.id
}

resource "authentik_stage_user_login" "user_login" {
  name = "user-login"
}
