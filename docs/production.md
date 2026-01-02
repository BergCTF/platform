---
icon: lucide/monitor-cloud
---
# Production

## VM

Provision a VM using the provider of your choice. Ensure DNS records are setup properly (both platform and wildcard challenge url entries):

```txt
.ctf.yourdomain.example 127.0.0.1
.ctf.yourdomain.example ::1
*.ctf.yourdomain.example 127.0.0.1
*.ctf.yourdomain.example ::1
```

## Firewall

Ensure that the following ports are open:

```
80/tcp    platform http
443/tcp   platform https
1337/tcp  challenge https
31337/tcp challenge tls -> tcp proxy
30000-65535/tcp nodeport services (if needed)
30000-65535/udp nodeport services (if needed)
```

## K3s Installation

```sh
curl -sfL https://get.k3s.io | INSTALL_K3S_EXEC='--flannel-backend=none --disable-network-policy --disable=traefik --tls-san <your-public-domain>' sh -
```

## Helm preparation

```sh
helm repo add dex https://charts.dexidp.io
helm repo add jetstack https://charts.jetstack.io
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo add jaegertracing https://jaegertracing.github.io/helm-charts
helm repo add cilium https://helm.cilium.io/
helm repo add traefik https://traefik.github.io/charts
helm repo add uptrace https://charts.uptrace.dev
helm repo update
```

## Cilium

```sh
cat <<EOF | helm install --wait cilium cilium/cilium -n cilium --version 1.17.4 --create-namespace -f -
ipam:
  mode: kubernetes
operator:
  replicas: 1
bandwidthManager:
  enabled: true
hubble:
  relay:
    enabled: false
  ui:
    enabled: false
  tls:
    auto:
      method: cronJob
EOF
```

## Traefik

```sh
kubectl apply -f https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.3.0/experimental-install.yaml
cat <<EOF | helm install --wait traefik traefik/traefik --version v35.4.0 -n traefik --create-namespace -f -
globalArguments:
  - "--global.checknewversion=false"
  - "--global.sendanonymoususage=false"
gateway:
  enabled: true
  name: "traefik-gateway"
  listeners:
    web:
      port: 8000
      protocol: HTTP
      namespacePolicy: All
    websecure:
      port: 8443
      protocol: HTTPS
      namespacePolicy: All
      certificateRefs:
        - kind: Secret
          name: berg-gateway-tls
      mode: Terminate
    http-chall:
      port: 1337
      protocol: HTTP
      namespacePolicy: All
    https-chall:
      port: 1337
      protocol: HTTPS
      namespacePolicy: All
      certificateRefs:
        - kind: Secret
          name: berg-gateway-tls
      mode: Terminate
    tls-chall:
      port: 31337
      protocol: TLS
      namespacePolicy: All
      certificateRefs:
        - kind: Secret
          name: berg-gateway-tls
      mode: Terminate
gatewayClass:
  enabled: true
providers:
  kubernetesIngress:
    publishedService:
      enabled: true
  kubernetesGateway:
    enabled: true
    experimentalChannel: true
service:
  type: LoadBalancer
ports:
  web:
    port: 8000
    exposedPort: 80
    nodePort: 30080
  websecure:
    port: 8443
    exposedPort: 443
    nodePort: 30443
  http-chall:
    protocol: TCP
    port: 1337
    exposedPort: 1337
    nodePort: 30337
    expose:
      default: true
  tls-chall:
    protocol: TCP
    port: 31337
    exposedPort: 31337
    nodePort: 31337
    expose:
      default: true
tlsOptions:
  default:
    sniStrict: false
    alpnProtocols:
      - http/1.1
EOF
```

### TLS Certificate

Install `cert-manager` to manage the creation and renewal of your gateway certificate.

Alternatively, you can also use `certbot` and do it manually:

```sh
certbot certonly --manual --preferred-challenges=dns -d '*.ctf.yourdomain.example' -d 'ctf.yourdomain.example'

k3s kubectl create secret tls -n traefik berg-gateway-tls --cert=/etc/letsencrypt/live/ctf.yourdomain.example/fullchain.pem --key=/etc/letsencrypt/live/ctf.yourdomain.example/privkey.pem
```

## ArgoCD

```sh
k3s kubectl create namespace argocd
k3s kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
cat <<EOF | k3s kubectl apply -n argocd -f -
apiVersion: v1
kind: ConfigMap
metadata:
  name: argocd-cm
  namespace: argocd
  labels:
    app.kubernetes.io/name: argocd-gpg-keys-cm
    app.kubernetes.io/part-of: argocd
data:
  resource.exclusions: |
    - apiGroups:
        - cilium.io
      kinds:
        - CiliumIdentity
      clusters:
        - "*"
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: argocd-cmd-params-cm
  namespace: argocd
  labels:
    app.kubernetes.io/name: argocd-gpg-keys-cm
    app.kubernetes.io/part-of: argocd
data:
  server.insecure: 'true'
---
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: argocd
  namespace: argocd
spec:
  hostnames:
    - argocd.ctf.yourdomain.example
  parentRefs:
    - group: gateway.networking.k8s.io
      kind: Gateway
      name: traefik-gateway
      namespace: traefik
      sectionName: websecure
  rules:
    - backendRefs:
        - group: ''
          kind: Service
          name: argocd-server
          port: 80
          weight: 1
      matches:
        - path:
            type: PathPrefix
            value: /
EOF
```

## Adding ArgoCD Applications

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: cnpg-operator
  namespace: argocd
spec:
  destination:
    namespace: cnpg-system
    server: https://kubernetes.default.svc
  project: default
  source:
    chart: cloudnative-pg
    repoURL: https://cloudnative-pg.github.io/charts
    targetRevision: 0.24.0
  syncPolicy:
    syncOptions:
      - CreateNamespace=true
```

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: berg-db
  namespace: argocd
spec:
  destination:
    namespace: berg
    server: https://kubernetes.default.svc
  project: default
  source:
    chart: cluster
    helm:
      values: |-
        cluster:
          instances: 1
          storage:
            size: 15Gi
    repoURL: https://cloudnative-pg.github.io/charts
    targetRevision: 0.3.1
  syncPolicy:
    syncOptions:
      - CreateNamespace=true
```

## Berg

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: berg
  namespace: argocd
spec:
  destination:
    namespace: berg
    server: https://kubernetes.default.svc
  project: default
  source:
    chart: berg
    helm:
      values: |
        gateway:
          domain: ctf.yourdomain.example
          name: "traefik-gateway"
          namespace: "traefik"
          httpListenerName: "web"
          httpsListenerName: "websecure"
          httpRouteRedirectListenerName: "http-chall"
          httpRouteListenerName: "https-chall"
          tlsRouteListenerName: "tls-chall"
        handout:
          enabled: false
        berg:
          domain: ctf.yourdomain.example
          pullSecretName: "berg-pull-secret"
          postgresql:
            existingSecret:
              name: "berg-db-cluster-app"
          discord:
            clientId: "CLIENT_ID"
            clientSecret: "CLIENT_SECRET"
            botToken: "DISCORD_BOT_TOKEN"
            notificationGuildId: "PUBLIC_DISCORD_SERVER"
            notificationChannelId: "PUBLIC_DISCORD_SOLVE_CHANNEL"
          ctf:
            eventName: "YOUR CTF"
            eventOrganiser: "YOUR ORGANIZERS"
            eventLogoUrl: https://yourdomain.example/logo.png
            challengeDomain: "ctf.yourdomain.example"
            start: "2000-01-01T00:00:00+00:00"
            end: "2099-12-31T00:00:00+00:00"
            teams: false
            allowAnonymousAccess: true
            scoring:
              numSolvesBeforeMinimum: 5
    repoURL: ghcr.io/norelect/charts
    targetRevision: 5.0.2
```

### Pull Secret

```sh
# Either create the pull secret from an existing file
kubectl create secret docker-registry berg-pull-secret -n berg --from-file=~/.docker/config.json
# Or create a new one from scratch
kubectl create secret docker-registry berg-pull-secret -n berg --docker-server=ghcr.io --docker-username=USER --docker-password=TOKEN
```
