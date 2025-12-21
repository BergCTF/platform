output "kubeconfig" {
  value     = module.talos.kubeconfig_data
  sensitive = true
}

output "raw_kubeconfig" {
  value     = module.talos.kubeconfig
  sensitive = true
}

output "talosconfig" {
  value     = module.talos.talosconfig
  sensitive = true
}
