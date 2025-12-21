resource "authentik_source_oauth" "discord" {
  lifecycle { enabled = var.bootstrap.authentication.discord.enabled }
  name              = "Discord"
  slug              = "discord"
  consumer_key      = var.bootstrap.authentication.discord.client_id
  consumer_secret   = var.bootstrap.authentication.discord.client_secret
  additional_scopes = "guilds guilds.members.read"

  provider_type       = "discord"
  authentication_flow = data.authentik_flow.default_source_authentication.id
  enrollment_flow     = data.authentik_flow.default_enrollment_flow.id
}

resource "authentik_policy_expression" "discord_role_mapping_enrollment" {
  lifecycle { enabled = var.bootstrap.authentication.discord.enabled }
  name       = "discord-enrollment-role-mapping"
  expression = <<-EOF
from authentik.core.models import Group
GUILD_API_URL = "https://discord.com/api/users/@me/guilds/{guild_id}/member"

### CONFIG ###
guild_id = "${var.bootstrap.authentication.discord.guild_id}"  # Discord guild ID for SSO federation
##############

# Ensure flow is only run during OAuth logins via Discord
if "source" not in context:
    return True
if context["source"].provider_type != "discord":
    return True

# Get the user-source connection object from the context, and get the access token
connection = context.get("goauthentik.io/sources/connection")
if not connection:
    return False
access_token = connection.access_token

guild_member_request = requests.get(
    GUILD_API_URL.format(guild_id=guild_id),
    headers={
        "Authorization": f"Bearer {access_token}",
    },
)
guild_member_info = guild_member_request.json()

# Ensure we are not being ratelimited
if guild_member_request.status_code == 429:
    ak_message(f"Discord is throttling this connection. Retry in {int(guild_member_info['retry_after'])}s")
    return False

# Ensure user is a member of the guild
if "code" in guild_member_info:
    if guild_member_info["code"] == 10004:
        ak_message("User is not a member of the guild")
    else:
        ak_create_event("discord_error", source=context["source"], code=guild_member_info["code"])
        ak_message("Discord API error, try again later.")
    return False

# Get all discord_groups
discord_groups = Group.objects.filter(attributes__discord_role_id__isnull=False)

# Filter matching roles based on guild_member_info['roles']
user_groups_discord_updated = discord_groups.filter(attributes__discord_role_id__in=guild_member_info["roles"])

# Set matchin_roles in flow context
request.context["flow_plan"].context["groups"] = user_groups_discord_updated

# Create event with roles added
ak_create_event(
    "discord_role_sync",
    user_discord_roles_added=", ".join(str(group) for group in user_groups_discord_updated),
)

return True
               EOF
}

resource "authentik_policy_expression" "discord_role_mapping_authentication" {
  lifecycle { enabled = var.bootstrap.authentication.discord.enabled }
  name       = "discord-authentication-role-mapping"
  expression = <<-EOF
from authentik.core.models import Group
GUILD_API_URL = "https://discord.com/api/users/@me/guilds/{guild_id}/member"

### CONFIG ###
guild_id = "${var.bootstrap.authentication.discord.guild_id}"  # Discord guild ID for SSO federation
##############

# Ensure flow is only run during OAuth logins via Discord
if "source" not in context:
    return True
if context["source"].provider_type != "discord":
    return True

# Get the user-source connection object from the context, and get the access token
connection = context.get("goauthentik.io/sources/connection")
if not connection:
    return False
access_token = connection.access_token

guild_member_request = requests.get(
    GUILD_API_URL.format(guild_id=guild_id),
    headers={
        "Authorization": f"Bearer {access_token}"
    },
)
guild_member_info = guild_member_request.json()

# Ensure we are not being ratelimited
if guild_member_request.status_code == 429:
    ak_message(f"Discord is throttling this connection. Retry in {int(guild_member_info['retry_after'])}s")
    return False

# Ensure user is a member of the guild
if "code" in guild_member_info:
    if guild_member_info["code"] == 10004:
        ak_message("User is not a member of the guild")
    else:
        ak_create_event("discord_error", source=context["source"], code=guild_member_info["code"])
        ak_message("Discord API error, try again later.")
    return False

# Get all discord_groups
discord_groups = Group.objects.filter(attributes__discord_role_id__isnull=False)

# Split user groups into discord groups and non discord groups
user_groups_non_discord = request.user.ak_groups.exclude(pk__in=discord_groups.values_list("pk", flat=True))
user_groups_discord = list(request.user.ak_groups.filter(pk__in=discord_groups.values_list("pk", flat=True)))

# Filter matching roles based on guild_member_info['roles']
user_groups_discord_updated = discord_groups.filter(attributes__discord_role_id__in=guild_member_info["roles"])

# Combine user_groups_non_discord and matching_roles
user_groups_updated = user_groups_non_discord.union(user_groups_discord_updated)

# Update user's groups
request.user.ak_groups.set(user_groups_updated)

# Create event with roles changed
ak_create_event(
    "discord_role_sync",
    user_discord_roles_before=", ".join(str(group) for group in user_groups_discord),
    user_discord_roles_after=", ".join(str(group) for group in user_groups_discord_updated),
)

return True
EOF
}

resource "authentik_policy_binding" "discord_role_mapping_enrollment" {
  lifecycle { enabled = var.bootstrap.authentication.discord.enabled }
  policy = authentik_policy_expression.discord_role_mapping_enrollment.id
  target = data.authentik_flow.default_enrollment_flow.id
  order  = 0
}

resource "authentik_policy_binding" "discord_role_mapping_authentication" {
  lifecycle { enabled = var.bootstrap.authentication.discord.enabled }
  policy = authentik_policy_expression.discord_role_mapping_authentication.id
  target = data.authentik_flow.default_source_authentication.id
  order  = 0
}
