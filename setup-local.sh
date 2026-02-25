#!/bin/sh

set -e

# Install kind
if ! type "kind" > /dev/null; then
    echo "Installing kind"
    curl -Lo ./kind https://kind.sigs.k8s.io/dl/v0.29.0/kind-linux-amd64
    chmod +x ./kind
    sudo mv ./kind /usr/local/bin/kind
fi

# Install mkcert
if ! type "mkcert" > /dev/null; then
    echo "Installing mkcert"
    sudo apt install mkcert libnss3-tools
    mkcert -install
fi

# Add helm repos
echo "Adding helm repos"
helm repo add dex https://charts.dexidp.io
helm repo add jetstack https://charts.jetstack.io
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo add jaegertracing https://jaegertracing.github.io/helm-charts
helm repo add cilium https://helm.cilium.io/
helm repo add traefik https://traefik.github.io/charts
helm repo add uptrace https://charts.uptrace.dev
helm repo add project-zot http://zotregistry.dev/helm-charts
helm repo update

echo "Recreating kind cluster"
kind delete cluster --name=berg-dev-cluster || echo "Nothing to delete"
cat << EOL | kind create cluster --config=-
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
name: berg-dev-cluster
networking:
  disableDefaultCNI: true
nodes:
- role: control-plane
  extraPortMappings:
  - containerPort: 30080
    hostPort: 80
    listenAddress: "127.0.0.1"
    protocol: TCP
  - containerPort: 30443
    hostPort: 443
    listenAddress: "127.0.0.1"
    protocol: TCP
  - containerPort: 30337
    hostPort: 1337
    listenAddress: "127.0.0.1"
    protocol: TCP
  - containerPort: 31337
    hostPort: 31337
    listenAddress: "127.0.0.1"
    protocol: TCP
EOL

kubectl --context kind-berg-dev-cluster cluster-info

echo "Installing cilium"
cat <<EOF | helm --kube-context kind-berg-dev-cluster install --wait cilium cilium/cilium -n cilium --version 1.17.4 --create-namespace -f -
ipam:
  mode: kubernetes
image:
  pullPolicy: IfNotPresent
operator:
  replicas: 1
bandwidthManager:
  enabled: true
hubble:
  enabled: true
  relay:
    enabled: true
  ui:
    enabled: true
    ingress:
      enabled: true
      annotations:
        cert-manager.io/cluster-issuer: mkcert
      className: traefik
      hosts:
        - hubble.localhost
      tls:
        - secretName: hubble-tls
          hosts:
            - hubble.localhost
EOF

echo "Installing traefik"
cat <<EOF | helm --kube-context kind-berg-dev-cluster install --wait traefik traefik/traefik --version 35.4.0 -n traefik --create-namespace -f -
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
  type: NodePort
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

echo "Installing Gateway API CRDs"
kubectl --context kind-berg-dev-cluster apply -f https://github.com/kubernetes-sigs/gateway-api/releases/download/v1.2.0/experimental-install.yaml

echo "Installing cert-manager"
helm --kube-context kind-berg-dev-cluster install --wait \
    cert-manager \
    jetstack/cert-manager \
    --version v1.17.2 \
    --create-namespace \
    --namespace cert-manager \
    --set ingressShim.defaultIssuerName=mkcert \
    --set ingressShim.defaultIssuerKind=ClusterIssuer \
    --set crds.enabled=true

echo "Deploying mkcert private key to cert-manager namespace"
kubectl --context kind-berg-dev-cluster create secret tls mkcert --namespace cert-manager --cert="${XDG_DATA_HOME:-$HOME/.local/share}/mkcert/rootCA.pem" --key="${XDG_DATA_HOME:-$HOME/.local/share}/mkcert/rootCA-key.pem"

# Configure mkcert ClusterIssuer and cert
cat <<EOF | kubectl --context kind-berg-dev-cluster create -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: mkcert
spec:
  ca:
    secretName: mkcert
---
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: berg-gateway-cert
  namespace: traefik
spec:
  secretName: berg-gateway-tls
  commonName: localhost
  dnsNames:
    - "berg.localhost"
    - "*.berg.localhost"
  ipAddresses:
    - 127.0.0.1
  issuerRef:
    name: mkcert
    kind: ClusterIssuer
EOF

echo "Installing mock idp"
cat <<'EOF' | helm --kube-context kind-berg-dev-cluster install --wait idp oci://ghcr.io/norelect/charts/mock-identity-provider --version 0.7.1 --create-namespace -n mock-idp -f -
issuer: https://idp.localhost
users:
  - id: player
    name: Player
    email: player@mock.idp
    roles:
      - player
  - id: author
    name: Author
    email: author@mock.idp
    roles:
      - author
  - id: admin
    name: Admin
    email: admin@mock.idp
    roles:
      - admin
ingress:
  annotations:
    cert-manager.io/cluster-issuer: mkcert
  enabled: true
  hosts:
    - host: idp.localhost
      paths:
        - path: /
          pathType: ImplementationSpecific
  tls:
    - hosts:
        - idp.localhost
      secretName: idp-tls
EOF

echo "Create berg namespace"
kubectl --context kind-berg-dev-cluster create ns berg || true

echo "Installing postgres db"
cat <<EOF | helm --kube-context kind-berg-dev-cluster install --wait berg-postgresql bitnami/postgresql -n berg -f -
auth:
  username: "berg"
  password: "password"
  postgresPassword: "postgres-password"
  database: "berg"
primary:
  resources:
    limits:
      cpu: "2"
      memory: "2Gi"
    requests:
      cpu: "0.1"
      memory: "300Mi"
readReplicas:
  resources:
    limits:
      cpu: "2"
      memory: "2Gi"
    requests:
      cpu: "0.1"
      memory: "300Mi"
EOF

echo "Installing zot"

cat <<EOF | helm --kube-context kind-berg-dev-cluster install -n zot --create-namespace zot project-zot/zot -f -
ingress:
  className: traefik
  enabled: true
  annotations:
    cert-manager.io/cluster-issuer: mkcert
  hosts:
    - host: zot.localhost
      paths:
        - path: /
          pathType: Prefix
  tls:
    - hosts:
        - zot.localhost
      secretName: zot-tls
service:
    type: ClusterIP
    port: 5000
EOF
