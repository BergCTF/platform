#!/bin/sh

set -e

echo "Copy docker pull creds"
kubectl --context kind-berg-dev-cluster delete secret berg-pull-secret -n berg || true
kubectl --context kind-berg-dev-cluster create secret generic berg-pull-secret \
    --from-file=.dockerconfigjson=/home/$USER/.docker/config.json \
    --type=kubernetes.io/dockerconfigjson -n berg

echo "Build image"
docker build -t kind.localhost/berg/api:local -f Berg.Api/Dockerfile .

echo "Transfer image"
kind load docker-image --name=berg-dev-cluster kind.localhost/berg/api:local

cd charts/berg
echo "Uninstalling berg"
helm --kube-context kind-berg-dev-cluster uninstall -n berg berg || echo "Nothing to uninstall"
echo "Installing berg"
cat <<EOF | helm --kube-context kind-berg-dev-cluster install --wait berg . -n berg -f -
gateway:
  domain: berg.localhost
  tlsSecretName: "berg-gateway-tls"
  gatewayClassName: "traefik"
berg:
  image:
    repository: "kind.localhost/berg/api"
    tag: local
  domain: berg.localhost
  tlsSecretName: "berg-gateway-tls"
  pullSecretName: "berg-pull-secret"
  logLevel:
    Berg.Api: Debug
  postgresql:
    host: "berg-postgresql"
    database: "berg"
    username: "berg"
    password: "password"
  genericOpenId:
    issuer: "https://dex.localhost"
    internalIssuer: "http://dex.dex.svc.cluster.local:5556"
    clientId: "berg-client"
    clientSecret: "berg-client-secret"
    scopes:
      - "profile"
      - "email"
    claims:
      id: "sub"
      name: "name"
      email: "email"
  openTelemetry:
    grpc:
      tracingEndpoint: http://jaeger-operator-jaeger-collector.jaeger.svc.cluster.local:4317
  ctf:
    teams: true

postgresql:
  enabled: false
EOF

echo "Deploying example challenges"
cat <<EOF | kubectl --context kind-berg-dev-cluster apply -f -
apiVersion: berg.norelect.ch/v1
kind: Challenge
metadata:
  name: nginx
  namespace: berg
spec:
  author: NoRelect
  flag: flag{test_flag}
  description: nginx
  difficulty: baby
  categories:
    - web
    - misc
  containers:
    - hostname: nginx
      image: nginx:latest
      environment:
        WHATEVER: Value
      resourceLimits:
        cpu: "1"
        memory: "100Mi"
      ports:
        - port: 80
          protocol: tcp
          appProtocol: http
          type: publicHttpRoute
---
apiVersion: berg.norelect.ch/v1
kind: Challenge
metadata:
  name: hidden-nginx
  namespace: berg
spec:
  author: NoRelect
  flag: flag{test_flag_hidden}
  description: nginx
  difficulty: baby
  hideUntil: "2099-01-01T00:00:00+00:00"
  categories:
    - web
    - misc
  containers:
    - hostname: nginx
      image: nginx:latest
      ports:
        - port: 80
          protocol: tcp
          appProtocol: http
          type: publicHttpRoute
EOF