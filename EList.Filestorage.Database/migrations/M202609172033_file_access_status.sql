-- Moderation access: Active (0) vs Blocked (1). Blob stays on disk; download denied except service-token.
ALTER TABLE public.file_info
    ADD COLUMN IF NOT EXISTS access_status smallint NOT NULL DEFAULT 0;

COMMENT ON COLUMN public.file_info.access_status IS '0=Active (normal visibility rules), 1=Blocked (moderation; service-token only)';

CREATE INDEX IF NOT EXISTS file_info_access_status_idx ON public.file_info (access_status);
