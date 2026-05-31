import { describe, it, expect } from 'vitest';
import { parseGameCode } from './gameUrl';

describe('parseGameCode', () => {
  it('extrait le code d’une URL /g/<code>', () => {
    expect(parseGameCode('/g/lamp-pear-house-sheep')).toBe('lamp-pear-house-sheep');
  });
  it('retourne null hors préfixe', () => {
    expect(parseGameCode('/')).toBeNull();
    expect(parseGameCode('/lobby')).toBeNull();
  });
  it('retourne null si le code est vide', () => {
    expect(parseGameCode('/g/')).toBeNull();
  });
  it('ignore un slash final', () => {
    expect(parseGameCode('/g/lamp-pear-house-sheep/')).toBe('lamp-pear-house-sheep');
  });
});
