#!/bin/bash

curl -sfL https://get.k3s.io | INSTALL_K3S_EXEC='--flannel-backend=none --disable-network-policy --disable=traefik' sh -

kubectl apply -f https://raw.githubusercontent.com/kubernetes-sigs/gateway-api/v1.1.0/config/crd/standard/gateway.networking.k8s.io_gatewayclasses.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes-sigs/gateway-api/v1.1.0/config/crd/standard/gateway.networking.k8s.io_gateways.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes-sigs/gateway-api/v1.1.0/config/crd/standard/gateway.networking.k8s.io_httproutes.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes-sigs/gateway-api/v1.1.0/config/crd/standard/gateway.networking.k8s.io_referencegrants.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes-sigs/gateway-api/v1.1.0/config/crd/standard/gateway.networking.k8s.io_grpcroutes.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes-sigs/gateway-api/v1.1.0/config/crd/experimental/gateway.networking.k8s.io_tlsroutes.yaml

helm repo add cilium https://helm.cilium.io/
helm repo add traefik https://traefik.github.io/charts
helm repo update

cat <<EOF > cilium-values.yaml
operator:
  replicas: 1
hubble:
  enabled: true
  relay:
    enabled: true
  ui:
    enabled: true
EOF
helm install cilium cilium/cilium --version 1.16.1 -n kube-system -f cilium-values.yaml
rm cilium-values.yaml

cat <<EOF > traefik-values.yaml
globalArguments: []
gateway:
  enabled: false
gatewayClass:
  enabled: true
providers:
  kubernetesIngress:
    publishedService:
      enabled: true
  kubernetesGateway:
    enabled: true
    experimentalChannel: true
experimental:
  kubernetesGateway:
    enabled: true
ports:
  http-chall:
    protocol: TCP
    port: 1337
    exposedPort: 1337
    expose:
      default: true
  tls-chall:
    protocol: TCP
    port: 31337
    exposedPort: 31337
    expose:
      default: true
tlsOptions:
  default:
    sniStrict: false
    alpnProtocols:
      - http/1.1
EOF
helm install traefik traefik/traefik --version 30.1.0 -n traefik --create-namespace -f traefik-values.yaml
rm traefik-values.yaml

cat <<EOF > challenge-gateway.yaml
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: http-redirect
  namespace: berg
spec:
  parentRefs:
  - name: challenge-gateway
    sectionName: http-redirect
  rules:
  - filters:
    - type: RequestRedirect
      requestRedirect:
        scheme: https
        statusCode: 301
---
apiVersion: gateway.networking.k8s.io/v1
kind: Gateway
metadata:
  name: challenge-gateway
  namespace: berg
spec:
  gatewayClassName: traefik 
  listeners:
  - name: http-redirect
    protocol: HTTP
    port: 1337
    allowedRoutes:
      namespaces:
        from: Selector
        selector:
          matchLabels:
            kubernetes.io/metadata.name: berg
  - name: http
    protocol: HTTPS
    port: 1337
    tls:
      mode: Terminate
      certificateRefs:
        - kind: Secret
          name: ctf-wildcard-cert
    allowedRoutes:
      namespaces:
        from: Selector
        selector:
          matchLabels:
            app.kubernetes.io/managed-by: berg
            app.kubernetes.io/component: challenge
  - name: tls
    protocol: TLS
    port: 31337
    tls:
      mode: Terminate
      certificateRefs:
        - kind: Secret
          name: ctf-wildcard-cert
    allowedRoutes:
      namespaces:
        from: Selector
        selector:
          matchLabels:
            app.kubernetes.io/managed-by: berg
            app.kubernetes.io/component: challenge
EOF
kubectl apply -f challenge-gateway.yaml
rm challenge-gateway.yaml
