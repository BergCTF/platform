resource "authentik_flow" "berg_recovery_flow" {
  name           = "berg-recovery"
  title          = "Recover Password"
  slug           = "berg-recovery"
  designation    = "recovery"
  authentication = "require_unauthenticated"
  background     = var.background_image
}

resource "authentik_policy_expression" "berg_recovery_skip_if_restored" {
  name       = "berg-recovery-skip-if-restored"
  expression = <<-EOF
    return bool(request.context.get('is_restored', True))
  EOF
}

resource "authentik_stage_email" "berg_recovery_email" {
  name                     = "user-email-recovery"
  template                 = "reset.html"
  subject                  = "[${var.ctf_name}] Reset your password"
  activate_user_on_success = true
  use_global_settings      = true
}

resource "authentik_stage_user_write" "berg_recovery_user_write" {
  name               = "berg-recovery-user-write"
  user_creation_mode = "never_create"
}

resource "authentik_stage_identification" "berg_recovery_identification" {
  name        = "berg-recovery-identification"
  user_fields = ["email", "username"]
}

resource "authentik_stage_prompt" "berg_recovery_prompt_password" {
  name = "berg-recovery-prompt-password"
  fields = [
    resource.authentik_stage_prompt_field.prompt_field_password.id,
    resource.authentik_stage_prompt_field.prompt_field_password_repeat.id,
  ]
}

resource "authentik_flow_stage_binding" "recovery_identification" {
  target                  = authentik_flow.berg_recovery_flow.uuid
  stage                   = authentik_stage_identification.berg_recovery_identification.id
  evaluate_on_plan        = true
  re_evaluate_policies    = true
  policy_engine_mode      = "any"
  invalid_response_action = "retry"
  order                   = 10
}

resource "authentik_flow_stage_binding" "recovery_email" {
  target                  = authentik_flow.berg_recovery_flow.uuid
  stage                   = authentik_stage_email.berg_recovery_email.id
  evaluate_on_plan        = true
  re_evaluate_policies    = true
  policy_engine_mode      = "any"
  invalid_response_action = "retry"
  order                   = 20
}

resource "authentik_flow_stage_binding" "recovery_password" {
  target                  = authentik_flow.berg_recovery_flow.uuid
  stage                   = authentik_stage_prompt.berg_recovery_prompt_password.id
  evaluate_on_plan        = true
  re_evaluate_policies    = false
  policy_engine_mode      = "any"
  invalid_response_action = "retry"
  order                   = 30
}

resource "authentik_flow_stage_binding" "recovery_user_write" {
  target                  = authentik_flow.berg_recovery_flow.uuid
  stage                   = authentik_stage_user_write.berg_recovery_user_write.id
  evaluate_on_plan        = true
  re_evaluate_policies    = false
  policy_engine_mode      = "any"
  invalid_response_action = "retry"
  order                   = 40
}

resource "authentik_flow_stage_binding" "recovery_user_login" {
  target                  = authentik_flow.berg_recovery_flow.uuid
  stage                   = authentik_stage_user_login.user_login.id
  evaluate_on_plan        = true
  re_evaluate_policies    = false
  policy_engine_mode      = "any"
  invalid_response_action = "retry"
  order                   = 50
}

resource "authentik_policy_binding" "recovery_binding_identification" {
  order   = 0
  target  = authentik_flow_stage_binding.recovery_identification.id
  policy  = authentik_policy_expression.berg_recovery_skip_if_restored.id
  negate  = false
  enabled = true
  timeout = 30
}

resource "authentik_policy_binding" "recovery_binding_email" {
  order   = 0
  target  = authentik_flow_stage_binding.recovery_email.id
  policy  = authentik_policy_expression.berg_recovery_skip_if_restored.id
  negate  = false
  enabled = true
  timeout = 30
}
