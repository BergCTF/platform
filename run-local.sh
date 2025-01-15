#!/bin/sh

set -e

echo "Build images"
docker build -t kind.localhost/berg/api:local -f backend/Berg.Api/Dockerfile backend
docker build -t kind.localhost/berg/frontend:local -f frontend/Dockerfile frontend
echo "Transfer berg images"
kind load docker-image --name=berg-dev-cluster kind.localhost/berg/api:local
kind load docker-image --name=berg-dev-cluster kind.localhost/berg/frontend:local

echo "Building handouts and challenge images"
docker build -t kind.localhost/challenges/handouts:local -f challenges/Dockerfile challenges
docker build -t kind.localhost/challenges/example-web-lfi:local -f challenges/example-web-lfi/challenge-src/Dockerfile challenges/example-web-lfi/challenge-src
docker build -t kind.localhost/challenges/example-web-rce:local -f challenges/example-web-rce/challenge-src/Dockerfile challenges/example-web-rce/challenge-src
echo "Transfer handouts and challenge images"
kind load docker-image --name=berg-dev-cluster kind.localhost/challenges/handouts:local
kind load docker-image --name=berg-dev-cluster kind.localhost/challenges/example-web-lfi:local
kind load docker-image --name=berg-dev-cluster kind.localhost/challenges/example-web-rce:local

cd charts/berg
echo "Uninstalling berg"
helm --kube-context kind-berg-dev-cluster uninstall -n berg berg || echo "Nothing to uninstall"
echo "Installing berg"
cat <<EOF | helm --kube-context kind-berg-dev-cluster install --wait berg . -n berg -f -
gateway:
  domain: berg.localhost
  tlsSecretName: "berg-gateway-tls"
  gatewayClassName: "traefik"
frontend:
  image:
    repository: "kind.localhost/berg/frontend"
    tag: local
handout:
  enabled: true
  image:
    repository: "kind.localhost/challenges/handouts"
    tag: local
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
    issuer: "https://idp.localhost"
    internalIssuer: "http://idp-mock-identity-provider.mock-idp.svc.cluster.local:80"
    clientId: "berg-client"
    clientSecret: "berg-client-secret"
    scopes:
      - "profile"
      - "email"
      - "role"
    claims:
      id: "sub"
      name: "name"
      email: "email"
  openTelemetry:
    grpc:
      tracingEndpoint: http://jaeger-operator-jaeger-collector.jaeger.svc.cluster.local:4317
  ctf:
    start: "$(date --date 'now + 1 minute' -Iseconds)"
    end: "$(date --date 'now + 10 minutes' -Iseconds)"
    teams: true
    allowAnonymousAccess: true
    scoring:
      numSolvesBeforeMinimum: 10

pages:
  extra:
    path: /extra
    title: Extra
    index: 1
    content: |
      Extra Page Content

postgresql:
  enabled: false
EOF

echo "Upgrading CRD's"
kubectl --context kind-berg-dev-cluster apply -f crds/challenge.yaml
kubectl --context kind-berg-dev-cluster apply -f crds/page.yaml

echo "Deploying example challenges"

cd ../../challenges
./create-challenges-yaml.py
kubectl --context kind-berg-dev-cluster apply -f all-challenges.yaml
