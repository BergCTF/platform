resource "authentik_brand" "branding" {
  domain              = var.authentik.domain
  branding_title      = var.authentik.branding.title
  branding_logo       = var.authentik.branding.logo
  branding_favicon    = var.authentik.branding.favicon
  default_application = (var.authentik.branding.default_application != null) ? authentik_application.oidc_app[var.authentik.branding.default_application].uuid : null
  flow_authentication = authentik_flow.berg_authentication_flow.uuid
}
