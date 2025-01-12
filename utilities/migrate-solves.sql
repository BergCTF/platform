-- This script allows to transfer players and solves of a berg instance to another berg
-- instance that uses a different UUID namespace.
-- To make this possible, the federated ids of the users MUST BE THE SAME on both instances.

CREATE OR REPLACE FUNCTION get_namespace_uuid()
RETURNS uuid
LANGUAGE SQL
IMMUTABLE
BEGIN ATOMIC
  -- Replace this UUID with the new namespace uuid
  RETURN CAST('00000000-0000-0000-0000-000000000000' AS uuid);
END;

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE OR REPLACE FUNCTION get_remapped_player_id(federated_id text)
RETURNS uuid
LANGUAGE SQL
IMMUTABLE
BEGIN ATOMIC
  RETURN uuid_generate_v5(get_namespace_uuid(), federated_id);
END;

-- Migrate players based on federated id.
SELECT 'INSERT INTO "Players" ("Id", "Name", "FederatedId", "CreatedAt", "Email") SELECT '''||get_remapped_player_id("FederatedId")||''','''||"Name"||''','''||"FederatedId"||''','''||"CreatedAt"||''','''||"Email"||''' WHERE NOT EXISTS (SELECT NULL FROM "Players" WHERE "FederatedId" = '''||"FederatedId"||''');' FROM "Players";

-- Migrate solves based on federated id.
SELECT 'INSERT INTO "Solves" ("Id", "SolvedAt", "PlayerId", "ChallengeId") VALUES ('''||"Solves"."Id"||''','''||"Solves"."SolvedAt"||''',(SELECT "Id" FROM "Players" as "p" WHERE "p"."FederatedId" = '''||"Players"."FederatedId"||'''),'''||"Solves"."ChallengeId"||''');' FROM "Solves" JOIN "Players" ON "Solves"."PlayerId" = "Players"."Id";

DROP FUNCTION get_remapped_player_id;
DROP FUNCTION get_namespace_uuid;

DROP EXTENSION "uuid-ossp";