CREATE TABLE users (
    id uuid PRIMARY KEY,
    name text NOT NULL CHECK (btrim(name) <> ''),
    role text NOT NULL CHECK (role IN ('operator', 'approver'))
);

CREATE TABLE applications (
    id uuid PRIMARY KEY,
    name text NOT NULL UNIQUE CHECK (btrim(name) <> '')
);

CREATE TABLE application_versions (
    id uuid PRIMARY KEY,
    application_id uuid NOT NULL REFERENCES applications (id),
    label text NOT NULL CHECK (btrim(label) <> ''),
    UNIQUE (application_id, id),
    UNIQUE (application_id, label)
);

CREATE TABLE promotions (
    id uuid PRIMARY KEY,
    application_id uuid NOT NULL,
    application_version_id uuid NOT NULL,
    target_environment text NOT NULL CHECK (target_environment IN ('dev', 'staging', 'production')),
    status text NOT NULL CHECK (status IN ('requested', 'approved', 'deploying', 'completed', 'cancelled', 'rolled_back')),
    requested_by uuid NOT NULL REFERENCES users (id),
    requested_at timestamptz NOT NULL,
    completed_at timestamptz,
    FOREIGN KEY (application_id, application_version_id)
        REFERENCES application_versions (application_id, id)
);

CREATE UNIQUE INDEX promotions_active_target
    ON promotions (application_id, target_environment)
    WHERE status IN ('requested', 'approved', 'deploying');

CREATE INDEX promotions_completed_target
    ON promotions (application_id, target_environment, completed_at DESC)
    WHERE status = 'completed';

CREATE INDEX promotions_application_history
    ON promotions (application_id, requested_at DESC, id DESC);

CREATE TABLE domain_events (
    id uuid PRIMARY KEY,
    sequence bigint GENERATED ALWAYS AS IDENTITY UNIQUE,
    promotion_id uuid NOT NULL REFERENCES promotions (id),
    type text NOT NULL CHECK (type IN (
        'promotion_requested',
        'promotion_approved',
        'deployment_started',
        'promotion_completed',
        'promotion_rolled_back',
        'promotion_cancelled'
    )),
    occurred_at timestamptz NOT NULL,
    actor_id uuid NOT NULL REFERENCES users (id),
    payload jsonb NOT NULL
);

CREATE INDEX domain_events_promotion_history
    ON domain_events (promotion_id, sequence);

CREATE TABLE event_deliveries (
    event_id uuid NOT NULL REFERENCES domain_events (id),
    consumer text NOT NULL CHECK (consumer IN ('audit', 'notification', 'release_notes')),
    status text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'completed', 'failed')),
    attempts integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    available_at timestamptz NOT NULL,
    locked_until timestamptz,
    lock_token uuid,
    finished_at timestamptz,
    last_error text,
    PRIMARY KEY (event_id, consumer)
);

CREATE INDEX event_deliveries_claimable
    ON event_deliveries (consumer, available_at)
    WHERE status = 'pending';

CREATE TABLE audit_log (
    event_id uuid PRIMARY KEY REFERENCES domain_events (id),
    type text NOT NULL,
    promotion_id uuid NOT NULL REFERENCES promotions (id),
    occurred_at timestamptz NOT NULL,
    actor_id uuid NOT NULL REFERENCES users (id),
    recorded_at timestamptz NOT NULL
);

CREATE TABLE release_notes_drafts (
    id uuid PRIMARY KEY,
    promotion_id uuid NOT NULL UNIQUE REFERENCES promotions (id),
    triggering_event_id uuid NOT NULL UNIQUE REFERENCES domain_events (id),
    content text NOT NULL CHECK (btrim(content) <> ''),
    breaking_changes jsonb NOT NULL,
    created_at timestamptz NOT NULL
);
