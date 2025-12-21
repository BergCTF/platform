output "generated_deploy_keys" {
  value     = resource.tls_private_key.generated_deploy_key
  sensitive = true
}
