-- Convertit games.id (et la FK game_results.game_id) de uuid vers text
-- pour les codes de partie lisibles (4 mots). Idempotent sur un schéma déjà en text.
--
-- La FK est créée en inline par 0001 (REFERENCES) → son nom auto-généré est
-- `game_results_game_id_fkey`. On droppe ce nom réel (et l'ancien nom explicite
-- au cas où) avant d'altérer le type, puis on la recrée à l'identique.
BEGIN;
ALTER TABLE public.game_results DROP CONSTRAINT IF EXISTS game_results_game_id_fkey;
ALTER TABLE public.game_results DROP CONSTRAINT IF EXISTS fk_game_results_games;
ALTER TABLE public.game_results ALTER COLUMN game_id TYPE text USING game_id::text;
ALTER TABLE public.games ALTER COLUMN id TYPE text USING id::text;
ALTER TABLE public.game_results
    ADD CONSTRAINT game_results_game_id_fkey
    FOREIGN KEY (game_id) REFERENCES public.games(id) ON DELETE CASCADE;
COMMIT;
