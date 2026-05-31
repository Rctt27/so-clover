-- PostgreSQL migration: create games table for SoClover
CREATE TABLE IF NOT EXISTS public.games (
    id text PRIMARY KEY,
    status text NOT NULL,
    language text NULL,
    phase_ends_at_utc timestamptz NULL,
    updated_at_utc timestamptz NOT NULL,
    payload_json jsonb NOT NULL
);

-- Useful indexes
CREATE INDEX IF NOT EXISTS ix_games_status ON public.games(status);
CREATE INDEX IF NOT EXISTS ix_games_updated_at_utc ON public.games(updated_at_utc);
CREATE INDEX IF NOT EXISTS ix_games_phase_ends_at_utc ON public.games(phase_ends_at_utc);

-- Note: xmin system column is used for optimistic concurrency; no explicit column needed.

-- PostgreSQL migration: create game_results table to store per-player scoring results
CREATE TABLE IF NOT EXISTS public.game_results (
    id uuid PRIMARY KEY,
    game_id text NOT NULL REFERENCES public.games(id) ON DELETE CASCADE,
    player_name text NOT NULL,
    board_is_guessed boolean NOT NULL,
    attempts integer NOT NULL,
    duration_seconds integer NOT NULL
);

-- Useful indexes for results
CREATE INDEX IF NOT EXISTS ix_game_results_game_id ON public.game_results(game_id);
