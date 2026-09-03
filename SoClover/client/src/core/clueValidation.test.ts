import { describe, it, expect } from 'vitest';
import { computeLocalClueValidity, supportsSemanticCheck, validateClueLocally } from './clueValidation';

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

describe('supportsSemanticCheck', () => {
  // Miroir de SemanticValidationSupport côté back : les trois dictionnaires livrés
  // ont un validateur, tout le reste retombe sur « pas de vérification ».
  it.each([
    ['Français_OFF', true],
    ['francais_off', true],
    ['English_(from_FR_OFF)', true],
    ['Portuguese_(from_FR_OFF)', true],
    // normalizeText retire les diacritiques : la graphie native passe par le même préfixe.
    ['Português', true],
    ['Klingon', false],
    ['', false],
  ])('%s → %s', (language, expected) => {
    expect(supportsSemanticCheck(language as string)).toBe(expected);
  });

  it('valide localement un indice portugais contenant un mot du board', () => {
    const result = validateClueLocally('mesada', ['mesa'], 'Portuguese_(from_FR_OFF)', true);
    expect(result.isValid).toBe(false);
    expect(result.errors[0].cardWord).toBe('mesa');
  });

  it("n'applique pas l'heuristique R2 (voyelle finale) en portugais", () => {
    // « mesa » → racine FR « mes » ⊂ « mesmo » : faux positif qu'on refuse de lever.
    const result = validateClueLocally('mesmo', ['mesa'], 'Portuguese_(from_FR_OFF)', true);
    expect(result.isValid).toBe(true);
  });
});
