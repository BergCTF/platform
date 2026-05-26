---
icon: lucide/log-in
---

# OIDC Configuration

Berg supports authentication via OIDC with any compliant identity provider.

Below, you can find a sample configuration with explanations:

```yaml
berg:
  genericOpenId:
    # Issuer URL. Berg will fetch .well-known/openid-configuration 
    issuer: "https://idp.localhost/application/o/berg/"
    # OIDC Client ID
    clientId: "berg"
    # OIDC Client Secret
    clientSecret: ""
    # OIDC Scopes to request. You might need to add 'groups' here if your IDP requires it
    scopes:
      - "profile"
      - "email"
    # OIDC claim mappings, if your IDP returns different claim names
    claims:
      id: sub
      name: name
      email: email
      role: role
    # Role mappings based on the 'role' claim value
    roles:
      player: berg-player
      admin: berg-admin
      author: berg-author
```


## Authentik
The following example shows you how to set up a sample setup using Authentik:

<!-- Based on https://integrations.goauthentik.io/development/forgejo/ -->
1. Log in to authentik as an administrator and open the authentik Admin interface.
2. Navigate to **Applications** > **Applications** and click **New Application** to open the application wizard.
    * **Application**: provide a descriptive name, an optional group for the type of application, the policy engine mode, and optional UI settings.
    * **Choose a Provider type**: select **OAuth2/OpenID Connect** as the provider type.
    * Configure the Provider: provide a name (or accept the auto-provided name), the authorization flow to use for this provider, and the following required configurations.
        * Note the **Client ID**, **Client Secret**, and **slug** values because they will be required later.
        * Set a `Strict` redirect URI to `https://<berg-frontend>/api/openid/federation-callback"`.
        * Select any available signing key.
        * Under **Advanced protocol settings** > **Selected Scopes**, add `authentik default OAuth Mapping: OpenID 'entitlements'`.
3. Click Submit to save the new application and provider.
4. Create entitlements
    * Open the newly created application and click on the **Application entitlements** tab on the top of the page, and then click on **Create entitlement**. Provide a name for the entitlement and then click on **Create**
    * Create `berg-player`, `berg-admin` and `berg-author`
    * Now, you can bind users to these entitlements
5. Create custom property mapping
    * Navigate to **Customization** > **Property Mappings** and click **Create**. Create a **Scope Mapping** with the following configurations:
        * **Name**: Choose a descriptive name (e.g. `authentik berg OAuth Mapping: OpenID 'role'`)
        * **Scope name**: `role`
        * **Expression**:
          ```py
          entitlement_names = {
              entitlement.name
              for entitlement in request.user.app_entitlements(provider.application)
          }
          berg_claims = {}

          berg_claims["role"] = "berg-player"
          if "berg-author" in entitlement_names:
            berg_claims["role"] = "berg-author"
          if "berg-admin" in entitlement_names:
            berg_claims["role"] = "berg-admin"
          return berg_claims
          ```
    * Click **Finish**.
6. Add the custom property mapping to the Berg provider
    * Navigate to **Applications** > **Providers** and click on the **Edit** icon of the Berg provider.
    * Under **Advanced protocol settings** > **Scopes** add the following scopes to **Selected Scopes**:
        * `authentik default OAuth Mapping: OpenID 'email'`
        * `authentik default OAuth Mapping: OpenID 'profile'`
        * `authentik default OAuth Mapping: OpenID 'openid'`
        * `authentik berg OAuth Mapping: OpenID 'role'`
    * Click **Update**
7. Configure the Berg Helm Chart:
  ```yaml
  berg:
    issuer: "https://authentik.tld/application/o/berg/"
    clientId: "<client id from earlier>"
    clientSecret: "<client secret from earlier>"
    scopes:
      - "profile"
      - "email"
      - "role"
    roles:
      player: berg-player
      admin: berg-admin
      author: berg-author
  ```


Berg should now support OIDC login and assign permissions based on the entitlements.
