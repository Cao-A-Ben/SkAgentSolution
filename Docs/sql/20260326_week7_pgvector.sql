-- Week7 pgvector baseline migration (idempotent)
-- Target: PostgreSQL + pgvector extension

CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS memory_chunks (
    chunk_id UUID PRIMARY KEY,
    conversation_id TEXT NOT NULL,
    run_id TEXT NOT NULL,
    persona TEXT NOT NULL,
    ts TIMESTAMPTZ NOT NULL,
    content TEXT NOT NULL,
    content_hash TEXT NOT NULL UNIQUE,
    embedding VECTOR(128) NOT NULL,
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_memory_chunks_conversation_ts
    ON memory_chunks (conversation_id, ts DESC);

-- hnsw requires newer pgvector; fallback to ivfflat when unavailable.
DO $$
BEGIN
    BEGIN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_memory_chunks_embedding_hnsw
                 ON memory_chunks USING hnsw (embedding vector_cosine_ops)';
    EXCEPTION WHEN OTHERS THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_memory_chunks_embedding_ivfflat
                 ON memory_chunks USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100)';
    END;
END $$;

-- Reference query:
-- SELECT content, metadata, embedding <-> :queryEmbedding AS distance
-- FROM memory_chunks
-- WHERE conversation_id = :conversationId
-- ORDER BY embedding <-> :queryEmbedding
-- LIMIT :topK;
