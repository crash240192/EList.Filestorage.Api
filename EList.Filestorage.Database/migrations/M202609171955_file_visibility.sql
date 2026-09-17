-- File visibility: Public (0) for guest download, Private (1) requires auth / service-token.
-- Existing files stay Public so current covers/avatars keep working for guests.
ALTER TABLE public.file_info
    ADD COLUMN IF NOT EXISTS visibility smallint NOT NULL DEFAULT 0;

COMMENT ON COLUMN public.file_info.visibility IS '0=Public (anonymous download), 1=Private (auth or service-token)';

CREATE INDEX IF NOT EXISTS file_info_visibility_idx ON public.file_info (visibility);
