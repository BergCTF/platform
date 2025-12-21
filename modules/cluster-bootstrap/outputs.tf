output "infra_deploy_key_pub" {
  value = tls_private_key.deploy_key.public_key_openssh
}

output "challenge_deploy_key_pub" {
  value = tls_private_key.challenge_deploy_key.public_key_openssh
}

# outputs to be consumed by Authentik module
output "argocd_client_secret" {
  value     = random_password.argocd_client_secret.result
  sensitive = true
}

output "grafana_client_secret" {
  value     = random_password.grafana_client_secret.result
  sensitive = true
}

output "berg_client_secret" {
  value     = random_password.berg_client_secret.result
  sensitive = true
}

output "harbor_client_secret" {
  value     = random_password.harbor_client_secret.result
  sensitive = true
}

output "authentik_bootstrap_token" {
  value     = random_password.authentik_bootstrap_token.result
  sensitive = true
}
