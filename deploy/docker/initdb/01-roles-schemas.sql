-- Application role: cannot bypass RLS (BR-PLT-01). Migrations run as `postgres` (owner), the app runs as `app`.
CREATE ROLE app LOGIN PASSWORD 'app_dev_pw' NOSUPERUSER NOBYPASSRLS;

CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;

DO $$
DECLARE s text;
BEGIN
  FOREACH s IN ARRAY ARRAY['platform','core','reporting','estimating','planning',
    'procurement','sourcing','cost','contracts','cash','subcontracts','docs',
    'closeout','intelligence']
  LOOP
    EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I', s);
    EXECUTE format('GRANT USAGE ON SCHEMA %I TO app', s);
    EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app', s);
    EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO app', s);
  END LOOP;
END $$;
