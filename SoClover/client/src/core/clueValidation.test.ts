import { describe, it, expect } from 'vitest';
import { computeLocalClueValidity, validateClueLocally } from './clueValidation';

describe('validateClueLocally', () => {
  it('rejette un indice contenant un mot du board (R1 sous-chaîne)', () => {
    const result = validateClueLocally('naturel', ['nature'], 'francais', true);
    expect(result.isValid).toBe(false);
    expect(result.errors[0].cardWord).toBe('nature');
  });

  it('accepte un indice sans relation avec les mots du board', () => {
    const result = validateClueLocally('voiture', ['nature'], 'francais', true);
    expect(result.isValid).toBe(true);
  });
});

describe('computeLocalClueValidity', () => {
  it("ne valide JAMAIS en mode lecture seule (phases Guessing/Scoring) — retourne null même pour un indice qui serait invalide", () => {
    // Régression : la 5e carte leurre du board de Guessing peut contenir un mot
    // proche d'un indice déjà rédigé ; la vérification sémantique ne doit pas
    // s'exécuter hors phase WritingClues.
    const result = computeLocalClueValidity(false, 'naturel', ['nature'], 'francais', true);
    expect(result).toBeNull();
  });

  it('valide normalement en mode éditable (phase WritingClues)', () => {
    const result = computeLocalClueValidity(true, 'naturel', ['nature'], 'francais', true);
    expect(result).not.toBeNull();
    expect(result!.isValid).toBe(false);
  });

  it('valide un indice correct en mode éditable', () => {
    const result = computeLocalClueValidity(true, 'voiture', ['nature'], 'francais', true);
    expect(result).not.toBeNull();
    expect(result!.isValid).toBe(true);
  });
});
