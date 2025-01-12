-- This script allows to remap all player ids of an existing berg instance to
-- use a different UUID namespace or to upgrade random player ids to use the new,
-- namespace based UUIDs.

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

CREATE OR REPLACE FUNCTION remap_player_id_without_recreating_constraint(constraint_name text, table_name text, column_name text)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    EXECUTE format('ALTER TABLE %I ADD COLUMN "NewPlayerId" uuid;', table_name);
    EXECUTE format('UPDATE %I SET "NewPlayerId" = (SELECT "NewPlayerId" FROM "Players" WHERE "Players"."Id" = %I.%I);', table_name, table_name, column_name);

    EXECUTE format('ALTER TABLE %I DROP CONSTRAINT %I;', table_name, constraint_name);
    EXECUTE format('UPDATE %I SET %I = "NewPlayerId";', table_name, column_name);
    EXECUTE format('ALTER TABLE %I DROP COLUMN "NewPlayerId";', table_name);
END;
$$;

CREATE OR REPLACE FUNCTION add_foreign_key(constraint_name text, table_name text, column_name text)
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
    EXECUTE format('ALTER TABLE %I ADD CONSTRAINT %I FOREIGN KEY (%I) REFERENCES "Players" ("Id");', table_name, constraint_name, column_name);
END;
$$;

CREATE TABLE foreign_player_id_keys AS SELECT
    tc.constraint_name as constraint_name,
    tc.table_name as table_name,
    kcu.column_name as column_name
FROM
    information_schema.table_constraints AS tc
    JOIN information_schema.key_column_usage AS kcu
        ON tc.constraint_name = kcu.constraint_name
        AND tc.table_schema = kcu.table_schema
    JOIN information_schema.constraint_column_usage AS ccu
        ON ccu.constraint_name = tc.constraint_name
        AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY' AND ccu.table_name ='Players';

ALTER TABLE "Players" ADD COLUMN "NewPlayerId" uuid;
UPDATE "Players" SET "NewPlayerId" = get_remapped_player_id("FederatedId");

SELECT remap_player_id_without_recreating_constraint(constraint_name, table_name, column_name) as remap_result FROM foreign_player_id_keys;

ALTER TABLE "Players" DROP CONSTRAINT "PK_Players";
UPDATE "Players" SET "Id" = "NewPlayerId";
ALTER TABLE "Players" DROP COLUMN "NewPlayerId";
ALTER TABLE "Players" ADD CONSTRAINT "PK_Players" PRIMARY KEY ("Id");

SELECT add_foreign_key(constraint_name, table_name, column_name) as remap_result FROM foreign_player_id_keys;

DROP TABLE foreign_player_id_keys;

DROP FUNCTION get_remapped_player_id;
DROP FUNCTION get_namespace_uuid;
DROP FUNCTION remap_player_id_without_recreating_constraint;
DROP FUNCTION add_foreign_key;

DROP EXTENSION "uuid-ossp";
