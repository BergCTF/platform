# Instance Controller

The Berg challenge-instance controller is a service that handles the lifecycle of challenge instances within a Kubernetes cluster. 
It allows us to further isolate the scope of permissions the Berg API (and thus the player-accessible surface of the application) has.  
All communication between the Berg API and the controller happens via the Kubernetes API and is thus secured using TLS.  
The challenge-instance controller is also deployable independently to Berg itself, allowing for use-cases such as multi-tenancy.

## Repository

https://github.com/bergcTF/challenge-instance-controller

## Deployment with Berg

Installing the berg helm chart by default also installs the challenge instance controller.

You can disable this behaviour by setting the `controller.enabled` variable on the chart:

```bash
helm install berg oci://ghcr.io/bergctf/charts/berg --set controller.enabled=false
```

## Deployment via Helm Chart

```bash
helm install challenge-instance-controller oci://ghcr.io/bergctf/charts/berg-controller
```

## ChallengeInstanceClasses

The controller helm chart ships with a `ChallengeInstanceClass`. In almost all cases, you will want to adjust various settings in this class.

You can configure these values using the `instanceClass` parameter (`controller.instanceClass` when deploying via the Berg chart).

```yaml
apiVersion: berg.norelect.ch/v1
kind: ChallengeInstanceClass
metadata:
  name: default
  namespace: berg
spec:
  # if this challenge instance class is the default
  default: true
  # default resources set on challenges if nothing else is requested
  defaultResources:
    cpuLimit: 1000m
    cpuRequest: 100m
    memoryLimit: 512Mi
    memoryRequest: 128Mi
  # default timeout for challenges
  defaultTimeout: 2h
  gateway:
    # base challenge domain
    domain: chall.berg.io
    # name of the kubernetes gateway API gateway hosting your challenges
    name: berg-gateway
    # namespaces of the gateway
    namespace: berg
    # name of the listener for http-based challenges (with termination at the gateway)
    httpListenerName: https-chall
    httpPort: 1337
    # name of the listener for tls-wrapped tcp challenges (with termination at the gateway)
    tlsListenerName: tls-chall
    tlsPort: 31337
  # image pull secrets and policy for challenge instances
  imagePull:
    policy: Always
    secretNames: []
  # cilium network configuration for the challenge namespace
  network:
    additionalHeadlessService: false
    egressBandwidth: 10M
    ingressBandwidth: 10M
```

## Multi-Cluster

Multi-Cluster setups are currently not supported.
In theory, this would allow you to provision berg and the instance controller in one cluster and spawn instances in another. 

This setup is not currently supported.

## RBAC

By default, the challenge instance controller is deployed with permissions to manage namespaces and create resources within them.

For a secure, multi-tenancy setup, access to namespaces should be restricted with an out of band method such as Kyverno and resource creation should be limited
to the namespaces using software such as eg. rbac-manager.

## Multi-Tenancy

By default, the controller allows setting all challenge options. 

For now, restriction of options on challenges can be done using `Kyverno`. (eg. to block the usage of certain system capabilities)

## Virtualisation

Virtualisation features are supported, but require a separate device plugin to expose `/dev/kvm` to the challenge containers.
For this, https://github.com/olfillasodikno/generic-cdi-plugin can be used.

TODO: add a guide for this

## Roadmap

A couple of important features are on the roadmap for implementation:
- Tenancy: Allow deploying the controller with restricted RBAC to only allow access to certain namespaces
- Challenge Dev: Allow creating more advanced network policies to control inter-service communication
- Tenancy: Disallow local network access even when internet access is allowed
- Tenancy: Allow restricting capabilities within `ChallengeInstanceClasses`
- Security: Multi-Cluster setups

Tenancy here refers to the ability of using the challenge instance controller to create a secure, multi-tenancy environments in which untrusted authors can develop their challenges.

