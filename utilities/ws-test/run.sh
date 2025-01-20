#!/bin/sh

docker build -t ws-test:latest .
docker run --rm -it -e API_KEY="${API_KEY}" -v "/home/${USER}/.local/share/mkcert/rootCA.pem:/certs/root.pem" --net host ws-test:latest
