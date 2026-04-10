-- Upgrade daily_suggestions uniqueness from (suggestion_date, persona_name)
-- to (suggestion_date, conversation_id).
--
-- Keep the most recent record for each conversation/day pair.

WITH ranked AS (
    SELECT ctid,
           ROW_NUMBER() OVER (
               PARTITION BY suggestion_date, conversation_id
               ORDER BY created_at DESC, run_id DESC
           ) AS rn
    FROM daily_suggestions
)
DELETE FROM daily_suggestions ds
USING ranked r
WHERE ds.ctid = r.ctid
  AND r.rn > 1;

ALTER TABLE daily_suggestions
    DROP CONSTRAINT IF EXISTS pk_daily_suggestions;

ALTER TABLE daily_suggestions
    ADD CONSTRAINT pk_daily_suggestions PRIMARY KEY (suggestion_date, conversation_id);

CREATE INDEX IF NOT EXISTS idx_daily_suggestions_persona
    ON daily_suggestions (persona_name, created_at DESC);
