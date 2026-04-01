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
    categories: ["web", "rev"]      # Categories, fist one is the primary category
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
### Dynamic flag settings
See [below](#dynamic-flags)
### Capabilities
### Bandwidth

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
