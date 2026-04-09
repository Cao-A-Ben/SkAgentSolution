CREATE TABLE IF NOT EXISTS daily_suggestions (
    suggestion_date date NOT NULL,
    suggestion text NOT NULL,
    run_id text NOT NULL,
    conversation_id text NOT NULL,
    persona_name text NOT NULL,
    profile_hash text NOT NULL,
    prompt_hash text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    event_log_path text NULL,
    CONSTRAINT pk_daily_suggestions PRIMARY KEY (suggestion_date, persona_name)
);

CREATE INDEX IF NOT EXISTS idx_daily_suggestions_created_at
    ON daily_suggestions (created_at DESC);

CREATE INDEX IF NOT EXISTS idx_daily_suggestions_conversation
    ON daily_suggestions (conversation_id, created_at DESC);
