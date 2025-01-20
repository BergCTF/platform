# berg

Berg CTF Platform

## Local development

To get started with berg development, you have to run the following command once to prepare the local environment (Make sure that ports `80, 443, 1337 & 31337` are unused):

```sh
./setup-local.sh
```

This will install a local kubernetes cluster within docker, with `cilium`, `traefik`, `mock-identity-provider`, `cert-manager` and `uptrace` pre-installed. You can access them here:

- https://hubble.localhost/
- https://idp.localhost/
- https://uptrace.localhost/

Whenever you want to deploy and test a change you have made, you can run:

```sh
./run-local.sh
```

This will rebuild the docker images locally and redeploy berg using the helm chart at `charts/berg`.

The Berg API will then run, with the docs available at: https://berg.localhost/swagger

## API Authentication

If you want to use the Berg API, you will need to login via the frontend once to generate an API key. From then on, you can authenticate directly against the Berg API:

```py
import requests

r = requests.post("https://berg.localhost/api/openid/token", data={
    "client_id": "berg-client",
    "grant_type": "password",
    "username": "<user-guid-here>",
    "password": "<api-key-here>"
})
print(r.json())
```
