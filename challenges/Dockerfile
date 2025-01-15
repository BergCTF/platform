FROM python:3 AS builder
WORKDIR /data
COPY . /data
RUN ./create-handouts.py

FROM nginx:latest

COPY --from=builder /data/handouts/ /usr/share/nginx/html/handouts/