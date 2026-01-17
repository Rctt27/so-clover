import { Role, GamePhase } from '../types/game';

export type Action = 
  | 'ConfigureLobby'
  | 'NavigateBoards'
  | 'SeeClues'
  | 'SetClue'
  | 'SubmitBoard'
  | 'PlaceGuessingCard'
  | 'SwapGuessingCards'
  | 'RotateCard'
  | 'ValidateGuessingBoard'
  | 'RotateBoard';


export interface UserPermissionsContext {
  role: Role;
  isGameAdmin: boolean;
  phase: GamePhase;
}

export const canPerform = (context: UserPermissionsContext, action: Action): boolean => {
  const { role, isGameAdmin, phase } = context;

  switch (action) {
    case 'ConfigureLobby':
      return isGameAdmin && phase === 'Lobby';

    case 'NavigateBoards':
      // Disponible uniquement durant la phase WritingClues pour les Spectators
      return role === 'Spectator' && phase === 'WritingClues';

    case 'SetClue':
    case 'SubmitBoard':
      // Durant WritingClues, seuls les PlayerWritingClue peuvent agir
      return role === 'PlayerWritingClue' && phase === 'WritingClues';

    case 'PlaceGuessingCard':
    case 'SwapGuessingCards':
    case 'RotateCard':
    case 'RotateBoard':
    case 'ValidateGuessingBoard':
      // Uniquement pour les Guessers durant la phase Guessing
      return role === 'PlayerGuesser' && phase === 'Guessing';

    case 'SeeClues':
      return phase === 'WritingClues' || phase === 'Guessing' || phase === 'Scoring';

    default:
      return false;
  }
};
