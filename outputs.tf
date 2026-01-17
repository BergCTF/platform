output "kubeconfig" {
  value     = module.cluster.raw_kubeconfig
  sensitive = true
}

output "talosconfig" {
  value     = module.cluster.talosconfig
  sensitive = true
}
