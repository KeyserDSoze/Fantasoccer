-- Local data reset only. Keep EF migration history and the schema intact.
-- This file is piped to psql by the guarded wrapper, never run at API startup.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$
DECLARE targets text;
BEGIN
    IF current_database() <> 'fantasoccer_local' OR current_user <> 'fantasoccer_local' THEN
        RAISE EXCEPTION 'Reset is allowed only for the dedicated local Fantasoccer database and role.';
    END IF;
    PERFORM pg_advisory_xact_lock(59127502874019);
    SELECT string_agg(format('%I.%I', schemaname, tablename), ', ' ORDER BY tablename)
      INTO targets FROM pg_tables
      WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory';
    IF targets IS NOT NULL THEN
        EXECUTE 'TRUNCATE TABLE ' || targets || ' RESTART IDENTITY CASCADE';
    END IF;
END $$;
COMMIT;
