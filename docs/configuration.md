# Helm Chart Configuration Reference

This document provides a complete reference for configuring the `berg` Helm chart. Each section below corresponds to values you can set in your `values.yaml` file.

---

## GatewayAPI Configuration

```yaml
gateway:
  domain: ""
  name: "default-gateway"
  namespace: "default"
  httpListenerName: "http"
  httpsPort: 443
  httpsListenerName: "https"
  httpRoutePort: 1337
  httpRouteRedirectListenerName: "http-chall"
  httpRouteListenerName: "https-chall"
  tlsRoutePort: 31337
  tlsRouteListenerName: "tls-chall"
```

Settings for the Kubernetes gateway API used to expose services.

* `gateway.domain`: Base domain used for routing.
* `gateway.name`: Name of the gateway resource.
* `gateway.namespace`: Namespace in which the gateway resides.
* `gateway.httpListenerName`, `httpsListenerName`: Names for HTTP/HTTPS listeners.
* `gateway.httpsPort`, `httpRoutePort`, `tlsRoutePort`: Ports for various protocols.
* `gateway.httpRouteListenerName`, `httpRouteRedirectListenerName`, `tlsRouteListenerName`: Route-specific listener references.

---

## Frontend Service

```yaml
frontend:
  enabled: true
  pullSecretName: ""
  image:
    repository: "ghcr.io/norelect/berg/frontend"
    imagePullPolicy: IfNotPresent
    tag: ""
  resources:
    limits:
      cpu: "1"
      memory: "500Mi"
    requests:
      cpu: "0.2"
      memory: "100Mi"
```

Settings for the web frontend.

* `frontend.enabled`: Whether to deploy the frontend.
* `frontend.pullSecretName`: Name of secret for pulling private frontend image.
* `frontend.image`: Image configuration for the frontend.
* `frontend.resources`: Resource requests and limits.

---

## Handout Service

```yaml
handout:
  enabled: false
  ...
```

Configuration for optional handout service. Structure mirrors the one for the `frontend` block. This will deploy a custom image that'll be used for accessing handouts. It should have a webserver listening on port `80`.

---

## Backend (Berg API)

### Image Settings

* `berg.image`: Container image for the Berg API.
* `berg.pullSecretName`: Pull secret name for pulling backend image
* `berg.extraEnv`: Define extra environment variables
* `berg.extraEnvFrom`: Read extra environment variables from Secret or ConfigMap ressource

### Configuration

* `berg.challengeImagePullPolicy`: Image pull policy for challenges.
* `berg.challengeInstanceTimeout`: Timeout for challenge instances (`HH:MM:SS` format).
* `berg.challengeRuntimeClassName`: Optional runtime class name.
* `berg.challengeIngressBandwidth`: Ingress bandwidth for challenges.
* `berg.challengeEgressBandwidth`: Egress bandwidth for challenges.
* `berg.challengeCpuLimit`, `challengeMemoryLimit`: Default resource limits for challenge containers.
* `berg.domain`: The main domain to run berg on

### Logging

```yaml
logLevel:
  Default: Information
  Microsoft.AspNetCore: Warning
  Microsoft.EntityFrameworkCore.Database: Warning
  OpenIddict: Warning
  Quartz: Warning
  System.Net.Http.HttpClient.OpenIddict: Warning
```

Fine-grained logging levels per namespace.

### Resources
```yaml
resources:
limits:
  cpu: "2"
  memory: "2Gi"
requests:
  cpu: "0.2"
  memory: "100Mi"
```

Defines the compute and memory resources for the Berg API server.

### PostgreSQL

Database connection configuration:

```yaml
postgresql:
  host: ""
  database: ""
  username: ""
  password: ""
```

### Player Identification

* `berg.playerIdNamespace`: Namespace UUID to use for player IDs

---

### Discord Integration

```yaml
discord:
  clientId: ""
  clientSecret: ""
  botToken: ""
  notificationGuildId: "0"
  ...
```

Credentials and settings for Discord-based notifications and authentication. See [Discord](Discord.md)

---

### OpenID Connect Authentication

```yaml
genericOpenId:
  issuer: ""
  internalIssuer: ""
  ...
```

Settings for generic OpenID Connect provider integration:

* `berg.genericOpenId.issuer`, `internalIssuer`: External/internal URLs for issuer.
* `berg.genericOpenId.clientId`, `clientSecret`: OAuth credentials.
* `berg.genericOpenId.scopes`: Requested OAuth scopes.
* `berg.genericOpenId.claims`: Mapping of OpenID claims to internal roles and fields.

For an example configuration, see [OIDC with Authentik](OIDC-Authentik.md)

---

### Observability

```yaml
openTelemetry:
  grpc:
    tracingEndpoint: null
    metricsEndpoint: null
    loggingEndpoint: null
```

OpenTelemetry endpoints for tracing, metrics, and logs, see [Metrics](Metrics.md)

---

### CTF Event Metadata

```yaml
ctf:
  eventName: "Berg CTF"
  eventOrganiser: "Berg Contributors"
  ...
```

Defines event branding and metadata:

* `berg.ctf.eventName`: CTF name
* `berg.ctf.eventOrganizer`: CTF Organizer
* `berg.ctf.eventLogoUrl`: Brand logo URL
* `berg.ctf.start`: CTF start time
* `berg.ctf.end` Logos and scheduling.
* `berg.ctf.allowAnonymousAccess`: Enables access without login.
* `berg.ctf.teams`: Whether team mode is enabled.
* `berg.ctf.playerAttributes`: Customizable player profile fields.
* `berg.ctf.scoring`: Scoring behavior and thresholds.

---

### Redirect URIs and CORS

* `berg.redirectUris`: URIs allowed for OAuth2 redirection.
* `berg.corsOrigins`: Allowed CORS origins.

---

## Custom Pages

```yaml
pages:
  home:
    path: /
    title: Home
    index: 0
    content: |
      Welcome to your berg installation!
```

Static page definitions rendered in the default frontend. You can add additional custom pages by extending this section.
