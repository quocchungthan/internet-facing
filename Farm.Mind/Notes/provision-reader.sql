-- Run as the AFFiNE database administrator with psql -d affine -f provision-reader.sql.
-- Only the columns used by Mind are granted. This does not modify AFFiNE schemas or data.
\set ON_ERROR_STOP on
CREATE ROLE affine_mind_reader LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
    CONNECTION LIMIT 10;
\password affine_mind_reader
ALTER ROLE affine_mind_reader SET default_transaction_read_only = on;
ALTER ROLE affine_mind_reader SET statement_timeout = '15s';
GRANT CONNECT ON DATABASE :"DBNAME" TO affine_mind_reader;
GRANT USAGE ON SCHEMA public TO affine_mind_reader;
GRANT SELECT (id, disabled, registered) ON public.users TO affine_mind_reader;
GRANT SELECT (user_id, provider, provider_namespace, provider_account_id) ON public.user_connected_accounts TO affine_mind_reader;
GRANT SELECT (id, name) ON public.workspaces TO affine_mind_reader;
GRANT SELECT (workspace_id, user_id, role, state) ON public.workspace_members TO affine_mind_reader;
GRANT SELECT (workspace_id, member_default_doc_role) ON public.workspace_access_policies TO affine_mind_reader;
GRANT SELECT (workspace_id, doc_id, member_default_role) ON public.doc_access_policies TO affine_mind_reader;
GRANT SELECT (workspace_id, doc_id, principal_type, principal_id, role) ON public.doc_grants TO affine_mind_reader;
GRANT SELECT (workspace_id, page_id, blocked) ON public.workspace_pages TO affine_mind_reader;
GRANT SELECT (workspace_id, guid, blob, updated_at) ON public.snapshots TO affine_mind_reader;
GRANT SELECT (workspace_id, guid, blob, created_at) ON public.updates TO affine_mind_reader;
