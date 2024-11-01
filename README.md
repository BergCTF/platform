# berg

Berg CTF Platform

## Local development

To get started with berg development, you have to run the following command once to prepare the local environment (Make sure that ports `80, 443, 1337 & 31337` are unused):

```sh
./setup-local.sh
```

This will install a local kubernetes cluster within docker, with `cilium`, `traefik`, `dex`, `cert-manager` and `jaeger` pre-installed. You can access them here:

- https://hubble.localhost/
- https://jaeger.localhost/

Whenever you want to deploy and test a change you have made, you can run:

```sh
./run-local.sh
```

This will rebuild the docker images locally and redeploy berg using the helm chart at `charts/berg`.

The Berg API will then run, with the docs available at: https://berg.localhost/swagger
