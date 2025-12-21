# opentofu-modules

These modules allow provisioning a functional cluster on Hetzner.

## Todo

Some things aren't currently included, such as:
- provisioning DNS (required for certs to work)
- provisioning Cloudflare Captchas
- provisioning AWS SES credentials (mails for Authentik)
- provisioning S3 buckets (Hetzner is long term a bad choice here as the buckets don't allow nice granularity easily)

There's probably a bunch of stuff we can disable assuming we don't want authentik password auth

