CREATE TABLE feedback (
    id TEXT PRIMARY KEY NOT NULL,
    text TEXT NOT NULL CHECK(length(text) BETWEEN 1 AND 2000),
    version TEXT NOT NULL,
    location TEXT NOT NULL,
    in_encounter INTEGER NOT NULL CHECK(in_encounter IN (0, 1)),
    world_day INTEGER NOT NULL,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);
CREATE INDEX feedback_created_at ON feedback(created_at);
