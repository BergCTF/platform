#!/bin/sh

set -e

echo "Copy docker pull creds"
kubectl --context kind-berg-dev-cluster delete secret berg-pull-secret -n berg || true
kubectl --context kind-berg-dev-cluster create secret generic berg-pull-secret \
    --from-file=.dockerconfigjson=/home/$USER/.docker/config.json \
    --type=kubernetes.io/dockerconfigjson -n berg

echo "Build images"
docker build -t kind.localhost/berg/api:local -f backend/Berg.Api/Dockerfile backend
docker build -t kind.localhost/berg/frontend:local -f frontend/Dockerfile frontend
docker build -t kind.localhost/berg/handout:local -f handout/Dockerfile handout

echo "Transfer images"
kind load docker-image --name=berg-dev-cluster kind.localhost/berg/api:local
kind load docker-image --name=berg-dev-cluster kind.localhost/berg/frontend:local
kind load docker-image --name=berg-dev-cluster kind.localhost/berg/handout:local

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
    repository: "kind.localhost/berg/handout"
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

postgresql:
  enabled: false
EOF

echo "Upgrading CRD's"
kubectl --context kind-berg-dev-cluster apply -f crds/challenge.yaml
kubectl --context kind-berg-dev-cluster apply -f crds/page.yaml

echo "Deploying example pages"
cat <<EOF | kubectl --context kind-berg-dev-cluster apply -f -
apiVersion: berg.norelect.ch/v1
kind: Page
metadata:
  name: home
  namespace: berg
spec:
  path: home
  title: Home
  index: 0
  content: |
    Home Page Content
---
apiVersion: berg.norelect.ch/v1
kind: Page
metadata:
  name: extra
  namespace: berg
spec:
  path: extra
  title: Extra
  index: 1
  content: |
    Extra Page Content
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
  tags:
    - nginx
    - http
  event: development
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
      dynamicFlag:
        env:
          name: FLAG
---
apiVersion: berg.norelect.ch/v1
kind: Challenge
metadata:
  name: another-nginx
  namespace: berg
spec:
  author: NoRelect
  flag: flag{test_flag}
  description: another-nginx
  difficulty: easy
  categories:
    - web
    - misc
  tags:
    - nginx
    - http
  event: development
  containers:
    - hostname: nginx
      image: nginx:latest
      ports:
        - port: 80
          protocol: tcp
          appProtocol: http
          type: publicHttpRoute
      dynamicFlag:
        content:
          path: /folder/flag.txt
---
apiVersion: berg.norelect.ch/v1
kind: Challenge
metadata:
  name: yet-another-nginx
  namespace: berg
spec:
  displayName: yet another nginx!
  author: NoRelect
  flag: flag{test_flag}
  description: yet-another-nginx
  difficulty: medium
  categories:
    - web
    - misc
  tags:
    - nginx
    - http
  event: development
  containers:
    - hostname: nginx
      image: nginxinc/nginx-unprivileged:latest
      ports:
        - port: 8080
          protocol: tcp
          appProtocol: http
          type: publicHttpRoute
      dynamicFlag:
        executable:
          path: /folder/runme
  attachments:
    - downloadUrl: /handouts/yet-another-nginx.tar.gz
      fileName: yet-another-nginx.tar.gz
---
apiVersion: berg.norelect.ch/v1
kind: Challenge
metadata:
  name: hard1-nginx
  namespace: berg
spec:
  author: NoRelect
  flag: flag{test_flag}
  description: hard-nginx
  difficulty: hard
  categories:
    - web
    - misc
  tags:
    - nginx
    - http
  event: development
  containers:
    - hostname: nginx
      image: nginx:latest
      ports:
        - port: 80
          protocol: tcp
          appProtocol: http
          type: publicHttpRoute
      dynamicFlag:
        executable:
          path: /runme
---
apiVersion: berg.norelect.ch/v1
kind: Challenge
metadata:
  name: hard2-nginx
  namespace: berg
spec:
  author: NoRelect
  flag: flag{test_flag}
  description: hard-nginx
  difficulty: hard
  categories:
    - web
    - misc
  tags:
    - nginx
    - http
  event: development
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
  tags:
    - nginx
    - http
  event: development
  containers:
    - hostname: nginx
      image: nginx:latest
      ports:
        - port: 80
          protocol: tcp
          appProtocol: http
          type: publicHttpRoute
EOF