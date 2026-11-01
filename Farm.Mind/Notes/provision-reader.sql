-- Run as the AFFiNE database administrator with psql -d affine -f provision-reader.sql.
-- Grants only the columns used by Mind and creates a secret-free OIDC configuration view.
-- Existing reader credentials and AFFiNE tables/data are preserved.
\set ON_ERROR_STOP on
SELECT EXISTS(SELECT 1 FROM pg_roles WHERE rolname = 'affine_mind_reader') AS reader_exists \gset
\if :reader_exists
\else
CREATE ROLE affine_mind_reader LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION
    CONNECTION LIMIT 10;
\password affine_mind_reader
\endif
ALTER ROLE affine_mind_reader SET default_transaction_read_only = on;
ALTER ROLE affine_mind_reader SET statement_timeout = '15s';
GRANT CONNECT ON DATABASE :"DBNAME" TO affine_mind_reader;
GRANT USAGE ON SCHEMA public TO affine_mind_reader;
GRANT SELECT (id, disabled, registered) ON public.users TO affine_mind_reader;
GRANT SELECT (user_id, provider, provider_account_id) ON public.user_connected_accounts TO affine_mind_reader;
SELECT EXISTS(SELECT 1 FROM pg_catalog.pg_attribute
    WHERE attrelid = 'public.user_connected_accounts'::regclass
      AND attname = 'provider_namespace' AND NOT attisdropped) AS namespaced \gset
\if :namespaced
GRANT SELECT (provider_namespace) ON public.user_connected_accounts TO affine_mind_reader;
\else
CREATE SCHEMA IF NOT EXISTS mind_note_reader;
CREATE OR REPLACE VIEW mind_note_reader.oidc_identity WITH (security_barrier = true) AS
    SELECT value->>'issuer' AS issuer, value->>'clientId' AS client_id,
        coalesce(nullif(value #>> '{args,claim_id}', ''), 'sub') AS claim_id
    FROM public.app_configs WHERE id = 'oauth.providers.oidc';
GRANT USAGE ON SCHEMA mind_note_reader TO affine_mind_reader;
GRANT SELECT ON mind_note_reader.oidc_identity TO affine_mind_reader;
\endif
GRANT SELECT (id, name) ON public.workspaces TO affine_mind_reader;
GRANT SELECT (workspace_id, user_id, role, state) ON public.workspace_members TO affine_mind_reader;
GRANT SELECT (workspace_id, member_default_doc_role) ON public.workspace_access_policies TO affine_mind_reader;
GRANT SELECT (workspace_id, doc_id, member_default_role) ON public.doc_access_policies TO affine_mind_reader;
GRANT SELECT (workspace_id, doc_id, principal_type, principal_id, role) ON public.doc_grants TO affine_mind_reader;
GRANT SELECT (workspace_id, page_id, blocked) ON public.workspace_pages TO affine_mind_reader;
GRANT SELECT (workspace_id, guid, blob, updated_at) ON public.snapshots TO affine_mind_reader;
GRANT SELECT (workspace_id, guid, blob, created_at) ON public.updates TO affine_mind_reader;
