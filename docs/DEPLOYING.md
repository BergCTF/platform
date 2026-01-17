# Deploying

'''note
For safety this guide does not include any tofu apply commands. Manually subsistute any tofu plan commands with tofu apply.
'''

After creating your infrastructure repository and configuring your tofu resources you will first want to create your cluster

Because we depend on the cluster to exist before we can roll out bootstrap resources to it, we run tofu with a resource exclusion:
```bash
tofu plan -var-file ./envs/myenv.tfvars -exclude module.berg-cluster.module.cluster_bootstrap
tofu plan -var-file ./envs/shc-2026-qual-prod.tfvars -exclude module.berg-cluster.module.authentik
```


