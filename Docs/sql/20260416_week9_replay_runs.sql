CREATE TABLE IF NOT EXISTS replay_runs (
    run_id text PRIMARY KEY,
    run_kind text NOT NULL,
    conversation_id text NOT NULL,
    status text NOT NULL,
    persona_name text NULL,
    goal text NULL,
    input_preview text NULL,
    final_output_preview text NULL,
    started_at timestamptz NOT NULL,
    finished_at timestamptz NULL,
    event_log_path text NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_replay_runs_started_at
    ON replay_runs (started_at DESC);

CREATE INDEX IF NOT EXISTS idx_replay_runs_kind_started_at
    ON replay_runs (run_kind, started_at DESC);

CREATE INDEX IF NOT EXISTS idx_replay_runs_conversation_started_at
    ON replay_runs (conversation_id, started_at DESC);
