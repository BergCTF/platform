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

## API Authentication

If you are not using the frontend that is running in the browser and want to use the Berg API, you will need to login via the frontend once to generate an API key. From then on, you can authenticate directly against the Berg API and use it:

```py
import requests

r = requests.post("https://berg.localhost/api/openid/token", data={
    "client_id": "berg-client",
    "grant_type": "password",
    "username": "<user-guid-here>",
    "password": "<api-key-here>"
})
print(r.json())
# Prints:
# {
#     'access_token': 'ey...',
#     'token_type': 'Bearer',
#     'expires_in': 1800,
#     'scope': 'profile roles openid offline_access',
#     'id_token': 'ey...',
#     'refresh_token': 'ey...'
# }
```
