-- Convertit games.id (et la FK game_results.game_id) de uuid vers text
-- pour les codes de partie lisibles (4 mots). Idempotent sur un schéma déjà en text.
ALTER TABLE public.game_results DROP CONSTRAINT IF EXISTS fk_game_results_games;
ALTER TABLE public.game_results ALTER COLUMN game_id TYPE text USING game_id::text;
ALTER TABLE public.games ALTER COLUMN id TYPE text USING id::text;
ALTER TABLE public.game_results
    ADD CONSTRAINT fk_game_results_games
    FOREIGN KEY (game_id) REFERENCES public.games(id) ON DELETE CASCADE;
