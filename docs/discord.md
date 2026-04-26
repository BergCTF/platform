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

## Granting permissions with roles

Berg grants permissions based on Discord guild (server) membership and roles. For each of the player, author, and admin tiers, set the corresponding `*GuildId` and `*RoleId`:

 * `playerGuildId` / `playerRoleId` — players
    * Players must have this role in order to register for the event
 * `authorGuildId` / `authorRoleId` — challenge authors
 * `adminGuildId` / `adminRoleId` — admins

The bot must be a member of each configured guild so it can look up role membership.
