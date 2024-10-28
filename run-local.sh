#!/bin/sh

set -e

echo "Copy docker pull creds"
kubectl --context kind-berg-dev-cluster delete secret berg-pull-secret -n berg || true
kubectl --context kind-berg-dev-cluster create secret generic berg-pull-secret \
    --from-file=.dockerconfigjson=/home/$USER/.docker/config.json \
    --type=kubernetes.io/dockerconfigjson -n berg

echo "Build image"
docker build -t kind.localhost/berg/challenge-server:local -f Berg.Api/Dockerfile .

echo "Transfer image"
kind load docker-image --name=berg-dev-cluster kind.localhost/berg/challenge-server:local

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
    repository: "kind.localhost/berg/challenge-server"
    tag: local
  domain: berg.localhost
  tlsSecretName: "berg-gateway-tls"
  pullSecretName: "berg-pull-secret"
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
      userId: "sub"
      userName: "name"
      userEmail: "email"

postgresql:
  enabled: false
  auth:
    username: "berg"
    password: "password"
    postgresPassword: "postgres-password"
    database: "berg"
EOF
