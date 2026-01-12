CREATE TABLE IF NOT EXISTS jobs (
    id UUID PRIMARY KEY,
    type TEXT NOT NULL,
    payload TEXT NOT NULL,
    status INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    run_at TIMESTAMPTZ NOT NULL,
    attempts INTEGER NOT NULL,
    max_attempts INTEGER NOT NULL,
    timeout_seconds INTEGER NOT NULL,
    lease_owner TEXT NULL,
    lease_expires_at TIMESTAMPTZ NULL,
    idempotency_key TEXT NULL,
    last_error TEXT NULL,
    result TEXT NULL,
    recurring_job_id UUID NULL
);

CREATE TABLE IF NOT EXISTS recurring_jobs (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    cron_expression TEXT NOT NULL,
    type TEXT NOT NULL,
    payload TEXT NOT NULL,
    next_run_at TIMESTAMPTZ NOT NULL,
    is_active BOOLEAN NOT NULL,
    idempotency_key TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS job_runs (
    id UUID PRIMARY KEY,
    job_id UUID NOT NULL REFERENCES jobs(id),
    attempt INTEGER NOT NULL,
    status INTEGER NOT NULL,
    worker_id TEXT NOT NULL,
    started_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ NULL,
    duration_ms INTEGER NULL,
    error TEXT NULL,
    result TEXT NULL
);

CREATE TABLE IF NOT EXISTS worker_heartbeats (
    id UUID PRIMARY KEY,
    worker_id TEXT NOT NULL UNIQUE,
    last_seen_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_jobs_status ON jobs(status);
CREATE INDEX IF NOT EXISTS ix_jobs_run_at ON jobs(run_at);
CREATE INDEX IF NOT EXISTS ix_jobs_lease_expires_at ON jobs(lease_expires_at);
CREATE INDEX IF NOT EXISTS ix_jobs_idempotency_key ON jobs(idempotency_key);

CREATE INDEX IF NOT EXISTS ix_recurring_jobs_next_run_at ON recurring_jobs(next_run_at);
CREATE INDEX IF NOT EXISTS ix_recurring_jobs_idempotency_key ON recurring_jobs(idempotency_key);

CREATE INDEX IF NOT EXISTS ix_job_runs_job_id ON job_runs(job_id);
CREATE INDEX IF NOT EXISTS ix_job_runs_status ON job_runs(status);
