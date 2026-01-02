---
icon: lucide/chart-spline
---
# Metrics

When onfiguring an OTEL endpoint in the berg chart values, the following
custom metrics get exported:

- `berg_instances_started`
- `berg_instances_count`
- `berg_submissions_valid`
- `berg_submissions_invalid`
- `berg_submissions_ratelimited`
- `berg_websockets_count`
- `berg_players_created`
- `berg_players_count`
- `berg_teams_created`
- `berg_teams_count`

Additionally, builtin metrics for ASP.NET and the `Npgsql` library are also sent.
