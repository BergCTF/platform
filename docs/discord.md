---
icon: simple/discord
---
# Discord Integration

Go to the [Discord Developer Portal](https://discord.com/developers/applications/) and create a new application, name it whatever you like.

Afterwards do the following:

**OAuth2** -> **General** -> **Redirects**

 * Add `https://ctf.example.com/api/openid/federation-callback`
 * Copy the **Client ID**
 * Hit "Reset Secret" and copy the **Client Secret**

**Bot**

 * Hit "Reset Token" and copy the **Bot Token**

See the [Configuration](helm.md/#discord-integration) documentation on how to configure Berg to use those values.

## Loading credentials from a Kubernetes Secret

Instead of putting the Client ID, Client Secret, and Bot Token directly into your Helm values (where they'd end up in `helm get values`, version control, etc.), you can store them in a Kubernetes Secret and point Berg at it via `berg.discord.existingSecret`.

Create the Secret in the same namespace as the Berg release:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: berg-discord
  namespace: berg
type: Opaque
stringData:
  clientId: "123456789012345678"
  clientSecret: "your-oauth-client-secret"
  botToken: "your-bot-token"
```

Then reference it in your Helm values:

```yaml
berg:
  discord:
    existingSecret:
      name: berg-discord
    notificationGuildId: "..."
    notificationChannelId: "..."
    # ...other non-sensitive discord settings
```

When `existingSecret.name` is set, the inline `clientId`, `clientSecret`, and `botToken` values are ignored.

If your Secret uses different key names, override the defaults:

```yaml
berg:
  discord:
    existingSecret:
      name: berg-discord
      clientIdKey: "DISCORD_CLIENT_ID"
      clientSecretKey: "DISCORD_CLIENT_SECRET"
      botTokenKey: "DISCORD_BOT_TOKEN"
```

## Granting permissions with roles

Berg grants permissions based on Discord guild (server) membership and roles. For each of the player, author, and admin tiers, set the corresponding `*GuildId` and `*RoleId`:

 * `playerGuildId` / `playerRoleId` — players
    * Players must have this role in order to register for the event
 * `authorGuildId` / `authorRoleId` — challenge authors
 * `adminGuildId` / `adminRoleId` — admins

The bot must be a member of each configured guild so it can look up role membership.
