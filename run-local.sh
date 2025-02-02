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
  name: "traefik-gateway"
  namespace: "traefik"
  httpListenerName: "web"
  httpsListenerName: "websecure"
  httpRouteRedirectListenerName: "http-chall"
  httpRouteListenerName: "https-chall"
  tlsRouteListenerName: "tls-chall"
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
  pullSecretName: "berg-pull-secret"
  challengeImagePullPolicy: "IfNotPresent"
  challengeInstanceTimeout: "0.00:01:00"
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
      tracingEndpoint: http://uptrace.uptrace.svc.cluster.local:14317
      loggingEndpoint: http://uptrace.uptrace.svc.cluster.local:14317
      metricsEndpoint: http://uptrace.uptrace.svc.cluster.local:14317
  extraEnv:
    - name: OTEL_EXPORTER_OTLP_HEADERS
      value: "uptrace-dsn=http://berg_uptrace_token@uptrace.uptrace.svc.cluster.local:14318?grpc=14317"
    - name: OTEL_EXPORTER_OTLP_COMPRESSION
      value: gzip
    - name: OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION
      value: BASE2_EXPONENTIAL_BUCKET_HISTOGRAM
    - name: OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE
      value: DELTA
  ctf:
    start: "$(date --date 'now + 1 minute' -Iseconds)"
    end: "$(date --date 'now + 10 minutes' -Iseconds)"
    teams: true
    allowAnonymousAccess: true
    scoring:
      numSolvesBeforeMinimum: 10
    playerAttributes:
      - name: category
        title: Category
        description: Select your player category.
        public: true
        required: true
        values:
          - value: junior
            title: Junior
            description: People between the age of 14 and 20.
          - value: senior
            title: Senior
            description: People between the age of 21 and 25.
          - value: open
            title: Open
            description: People over the age of 25.
      - name: country
        title: Country
        description: Select your country.
        public: true
        required: true
        values:
          - value: switzerland
            title: Switzerland
            description: You are a citizen of Switzerland.
          - value: liechtenstein
            title: Liechtenstein
            description: You are a citizen of Liechtenstein.
          - value: world
            title: World
            description: You are neither a citizen of Switzerland or Liechtenstein.
      - name: notifications
        title: Notifications
        description: Do you want to get notified about Berg updates?
        public: false
        required: false
        values:
          - value: "yes"
            title: "Yes"
            description: Receive updates about berg development.
          - value: "no"
            title: "No"
            description: Opt out of notifications.

pages:
  extra:
    path: /extra
    title: Extra
    index: 1
    content: |
      Extra Page Content
EOF

echo "Upgrading CRD's"
kubectl --context kind-berg-dev-cluster apply -f crds/challenge.yaml
kubectl --context kind-berg-dev-cluster apply -f crds/page.yaml

echo "Deploying example challenges"

cd ../../challenges
./create-challenges-yaml.py
kubectl --context kind-berg-dev-cluster apply -f all-challenges.yaml
