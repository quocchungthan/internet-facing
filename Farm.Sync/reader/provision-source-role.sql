-- Run as the AFFiNE database owner. This does not modify AFFiNE documents.
SELECT 'CREATE ROLE affine_sync LOGIN'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'affine_sync') \gexec
GRANT CONNECT ON DATABASE affine TO affine_sync;
GRANT USAGE ON SCHEMA public TO affine_sync;
GRANT SELECT ON TABLE public.snapshots, public.updates TO affine_sync;
