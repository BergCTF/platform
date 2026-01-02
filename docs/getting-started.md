---
icon: lucide/power
---
## Setting up testing environment

!!! warning
    This guide assumes you have Docker installed.

First, clone the Berg repository on your local machine:
```bash
git clone git@github.com:NoRelect/berg && cd berg
```
Then, prepare the test cluster with:
```bash
./setup-local.sh
``` 

Finally, you can start the test cluster:
```bash
./run-local.sh
```

The test environment comes with the following components:

* **Berg API and Frontend** ([https://berg.localhost](https://berg.localhost))
* **Cilium Hubble** for debugging network connections  ([https://hubble.localhost/](https://hubble.localhost/))
* **Mock Identity Provider** for demo authentication ([https://idp.localhost/](https://idp.localhost/))
* **Uptrace** for demonstrating tracing ([https://idp.localhost/](https://idp.localhost/))

You can now access Berg at [https://berg.localhost](https://berg.localhost) and play around with the example challenges.


## Writing a challenge
Let's now write a custom challenge. Berg challenges are always defined in a `challenge.yaml` file, which is a Kubernetes manifest for the `Challenge` CRD.

Consider the following manifest:
```yaml
apiVersion: berg.norelect.ch/v1
kind: Challenge
metadata:
  name: berger-king
  namespace: berg
spec:
  author: hacker
  flag: flag{example_flag}
  flagFormat: flag{...}
  description: Do you like bergers?
  difficulty: baby
  categories:
    - web
  tags:
    - rce
    - python
  containers:
    - hostname: web
      image: alpine:latest
      ports:
        - port: 80
          protocol: tcp
          appProtocol: http
          type: publicHttpRoute
      resourceRequests:
        cpu: "1"
        memory: "0.5Gi"
  attachments: []
```

First, we have the metadata of the challenge. You shouldn't need to change anything except for the challenge name:
```yaml
apiVersion: berg.norelect.ch/v1  # Kubernetes API version
kind: Challenge                  # Resource type
metadata:
  name: berger-king              # Your challenge name
  namespace: berg                # The default berg namespace
spec:
  ...
```

The challenge details are specified in the `spec` fields:
```yaml
  author: hacker                             # CTF author displayed on the platform
  flag: flag{example_flag}                   # The full flag
  flagFormat: flag{...}                      # The flag format shown to players
  description: Do you like <i>bergers</i>?   # The description, can contain HTML markup
  difficulty: baby                           # The displayed difficulty
  categories:
    - web                                    # Challenge categories, first one is considered the primary one
  tags:
    - rce                                    # Tags displayed on the plaform
    - python
```

There's a special array for the containers of the challenge instance:
```yaml
  containers:                    # This is a list; you may define multiple containers
    - hostname: web              # The hostname for this container, displayed to the user
      image: alpine:latest       # The docker image to use, we'll just use a dummy alpine one
      ports:                     # List of ports to expose to the user
        - port: 80               # App is listening on port 80
          protocol: tcp          # Our app uses tcp for connections
          appProtocol: http      # This causes Berg to display a clickable link instead of a `ncat` command
          type: publicHttpRoute  # This exposes our web service using an Ingress
      resourceRequests:          # We can limit the challenge resources to prevent resource exhaustion on the host
        cpu: "1"
        memory: "0.5Gi"
```

You can also specify attachments for challenges, see [Attachments](attachments.md) for this:
```yaml
  attachments: []  # Empty for now
```





To see all available options for a challenge, see the separate [Challenge](challenge.md) documentation.


## Testing out the challenge
You can now use `kubectl` to apply the challenge in Kubernetes, where Berg will automatically pick it up and display it in its UI:
```bash
kubectl --namespace berg apply --file ./challenge.yaml
```
