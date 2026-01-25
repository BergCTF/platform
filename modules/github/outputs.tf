output "generated_deploy_keys" {
  value     = resource.tls_private_key.generated_deploy_key
  sensitive = true
}

output "argocd_webhook_secret" {
  value     = resource.random_password.argocd_webhook_secret.result
  sensitive = true
}
