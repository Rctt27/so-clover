export type Role = 'Spectator' | 'PlayerBoardOwner' | 'PlayerGuesser' | 'PlayerWritingClue';

export type GamePhase = 'Initial' | 'Lobby' | 'WritingClues' | 'Guessing' | 'Scoring';

export type ConnectionStatus = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';

// Backend API Response Types
export interface GameStateResponse {
  gameId: string;
  language: string;
  cluesDurationSecondsOverride: number | null;
  guessDurationSecondsOverride: number | null;
  phase: GamePhase;
  adminPlayerId: string | null;
  phaseEndsAtUtc: string | null;
  revision: number;
  players: PlayerStateResponse[];
  guessingState: GuessingPhaseStateResponse | null;
  semanticClueCheckEnabled: boolean;
  guessAiBoardOnly: boolean;
}

export interface PlayerStateResponse {
  playerId: string;
  name: string;
  isAI: boolean;
  cursorColorIndex: number;
  board: BoardStateResponse;
}

export interface BoardStateResponse {
  top: DirectionStateResponse;
  right: DirectionStateResponse;
  bottom: DirectionStateResponse;
  left: DirectionStateResponse;
  isSubmitted: boolean;
}

export interface DirectionStateResponse {
  direction: string; // "Top", "Right", "Bottom", "Left"
  hasCard: boolean;
  isGuessed: boolean;
  clueLabel: string | null;
  expectedWord: string | null;
  card: CardInfoResponse | null;
}

export interface CardInfoResponse {
  cardId: string;
  topWord: string;
  rightWord: string;
  bottomWord: string;
  leftWord: string;
  rotation: string; // "None", "Clockwise90", "Clockwise180", "Clockwise270"
}

export interface FailedPlacementInfo {
  position: string; // "TopLeft" | "TopRight" | "BottomRight" | "BottomLeft"
  cardId: string;
  rotation: string; // "None" | "Right90" | "Right180" | "Right270" (also accepts "Clockwise*" variants)
}

export interface GuessingPhaseStateResponse {
  currentBoardOwnerId: string | null;
  currentBoardOwnerName: string | null;
  outsideCards: (CardInfoResponse | null)[];
  guessedPositions: Record<string, CardInfoResponse | null>;
  correctlyPlacedPositions: string[];
  remainingAttempts: number;
  currentBoardClues: ClueInfoResponse[];
  cumulativeBoardRotation: number;
  failedPlacements: FailedPlacementInfo[];
}

export interface ClueInfoResponse {
  direction: string;
  text: string;
  // LLM reasoning for AI-authored clues. Server only populates this once the current
  // Guessing board is resolved (success or attempts exhausted). Null for human clues
  // or while attempts remain — see backend GetGameState.cs.
  explanation: string | null;
}

export interface ScoringBoardResponse {
  playerId: string;
  playerName: string;
  attempts: number;
  durationSeconds: number;
  isDisconnected: boolean;
}

export interface GameScoringResponse {
  successfulBoards: ScoringBoardResponse[];
  failedBoards: ScoringBoardResponse[];
}

// Internal Client-side Types
export interface CardData {
  words: [string, string, string, string]; // [top, right, bottom, left]
  rotation: number; // in degrees: 0, 90, 180, 270
}

export interface CluePosition {
  text: string;
  playerId: string | null;
}

export interface BoardData {
  cards: (CardData | null)[]; // 4 cards in positions [TopLeft, TopRight, BottomLeft, BottomRight]
  rotation: number;
  isSubmitted: boolean;
  clues: {
    top: CluePosition;
    right: CluePosition;
    bottom: CluePosition;
    left: CluePosition;
  };
}

// Utility function to convert backend rotation to degrees
export function rotationToDegrees(rotation: string): number {
  switch (rotation) {
    case 'None': return 0;
    case 'Clockwise90': 
    case 'Right90': return 90;
    case 'Clockwise180': 
    case 'Right180': return 180;
    case 'Clockwise270': 
    case 'Right270': return 270;
    default: return 0;
  }
}

export interface ClueValidationErrorResponse {
  rule: 'ExactMatch' | 'SimilarStem'
  cardWord: string
  conflictingDirection?: 'Top' | 'Right' | 'Bottom' | 'Left' | null
}

export interface ClueValidationResponse {
  isValid: boolean
  errors: ClueValidationErrorResponse[]
}

export class ClueValidationRejection extends Error {
  errors: ClueValidationErrorResponse[]
  constructor(errors: ClueValidationErrorResponse[]) {
    super('Clue rejected')
    this.name = 'ClueValidationRejection'
    this.errors = errors
  }
}
