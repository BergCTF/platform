---
icon: lucide/code-xml
---
# Challenge

This page explains some extra features of the `Challenge` CRD.

## Static metadata
The following fields do not affect the challenge behaviour in any way:
```yaml
spec:
    # ...
    displayName: 'Free text!'       # Allows for any custom challenge names, unrelated to the Kubernetes name
    author: admin                   # Author username
    description: |
        <b>Challenge description</b><br>
        <marquee>Supports HTML!</marquee>
    flag: flag{1337}                # The flag
    flagFormat: flag{...}           # Flag format shown to the user
    categories: ["web", "rev"]      # Categories, first one is the primary category
    tags: ["xss", "bot"]            # Challenge tags
    event: "Demo CTF"               # Allows for logical grouping of challenges by event
```

## Hiding challenges
To release a challenge based on a schedule, you can use the `hideUntil` feature to only make it visible after a set time and date:
```yaml
spec:
    # ...
    hideUntil: 2025-10-01T18:00:00+02:00
```

## Outbound traffic
Challenges deny outbound traffic to the internet by default. To change this, you need to set `allowOutboundTraffic` to `true`.
```yaml
spec:
    # ...
    allowOutboundTraffic: true  # this can be useful for allowing exfiltration over the internet
```

## Containers

### Resources
You can specify resource `requests` and `limits` per challenge. 
* **Limits** are a hard limitation; if a container exceeds memory limits, it will get OOM killed
* **Requests** tell Kubernetes how many resources should be reserved for a container, especially useful in a cluster setup.

Example:
```yaml
spec:
    containers:
        - name: test
          # ...
          resourceLimits: 
            cpu: "0.4"
            memory: "800Mi"
          resourceRequests: 
            cpu: "0.2"
            memory: "400Mi"
```
### Ports

Each container can expose ports to different audiences using the `ports` field:

```yaml
spec:
  containers:
    - name: app
      # ...
      ports:
        - name: web          # Optional name, used to reference this port's URL
          port: 8080
          protocol: tcp
          appProtocol: http
          type: publicHttpRoute
```

The `type` field controls how the port is exposed:

| Type | Description |
|---|---|
| `internalPort` | Reachable only within the cluster (default) |
| `publicPort` | Exposed as a NodePort for direct TCP access |
| `publicHttpRoute` | Exposed via HTTP Gateway with a unique subdomain per deployment |
| `publicTlsRoute` | Exposed via TLS passthrough Gateway with a unique subdomain per deployment |

For `publicHttpRoute` and `publicTlsRoute`, berg generates a random UUID subdomain each time the challenge is deployed (e.g. `550e8400-e29b-41d4-a716-446655440000.ctf.example.com`). If the port has a `name`, players can see the URL for that specific port.

### Dynamic flag settings
See [below](#dynamic-flags)

### Capabilities

By default, containers run without any additional Linux capabilities. You can grant extra capabilities with `additionalCapabilities`:

```yaml
spec:
  containers:
    - name: app
      # ...
      additionalCapabilities:
        - NET_ADMIN
        - SYS_PTRACE
```

Specify capability names without the `CAP_` prefix. Containers are never privileged, but `allowPrivilegeEscalation` is enabled to support setuid/setgid binaries.

> [!NOTE]
> When using an [executable dynamic flag](#executable-binary), berg automatically drops `CAP_DAC_OVERRIDE` to prevent root users from reading the flag binary. If your image requires `DAC_OVERRIDE`, add it to `additionalCapabilities` to override this behaviour.

### Bandwidth

Berg limits per-container network bandwidth to protect shared infrastructure. The defaults are configured by the platform operator. You can override them per container:

```yaml
spec:
  containers:
    - name: app
      # ...
      egressBandwidth: "10M"   # Outbound limit (default: 1M)
      ingressBandwidth: "10M"  # Inbound limit (default: 1M)
```

Values follow the Kubernetes resource quantity format: `K`, `M`, `G` for kilobits, megabits, and gigabits per second respectively.

## Dynamic Flags

Its possible to let berg inject dynamic flags into a deployed challenge. This can be done via the new `dynamicFlag` property of the `Challenge` CRD on the `container` spec in one of three ways:

### Environment Variable

```yaml
dynamicFlag:
  env:
    name: FLAG
```

### File Content

```yaml
dynamicFlag:
  content:
    path: /folder/flag.txt
    mode: 0o444 # r--r--r--
```

Any occurence of `{entropy}` in the `path` variable will be replaced with a random hex string that changes on each challenge start.

### Executable Binary

```yaml
dynamicFlag:
  executable:
    path: /folder/runme
    mode: 0o111 # --x--x--x
```

Any occurence of `{entropy}` in the `path` variable will be replaced with a random hex string that changes on each challenge start.

> [!NOTE]
> Using this will drop `CAP_DAC_OVERRIDE`, which may break some container images

### Full example

```yaml
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
  tags:
    - nginx
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
```

When printing the flag, you can see that the dynamic flag modifies the flag specified in the `flag` property to contain random data:

```console
root@nginx:/# ls -lah /folder/flag.txt
-r--r--r-- 1 root root 29 Jan  8 23:09 /folder/flag.txt
root@nginx:/# cat /folder/flag.txt
flag{test_flag_da3cdd0ac0f9}
root@nginx:/#
```

## Attachments
See the dedicated [attachments](attachments) page.
