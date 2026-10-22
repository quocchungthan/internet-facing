import { describe, expect, it } from 'vitest';
import { vehicles } from '../../../content';
import { createVehicleItems } from '../../exploration/domain/items';
import { chatGptQuestionUrl, componentQuestion } from './componentQuestion';

const vehicle = vehicles[0];
const tree = createVehicleItems(vehicle);

describe('public component questions', () => {
  it('uses canonical occurrences, including different repeated installations', () => {
    const left = componentQuestion(vehicle, tree, 'wheel-front-left');
    const right = componentQuestion(vehicle, tree, 'wheel-rear-right');
    expect(left).toContain('front left');
    expect(right).toContain('rear right');
    expect(left).not.toBe(right);
    expect(left).toContain(vehicle.name);
    expect(left).toContain('does not establish exact fitment');
    expect(left).toContain('not repair instructions');
  });

  it('does not invent specificity for unknown or whole-car subjects', () => {
    for (const id of ['missing', 'vehicle']) {
      const question = componentQuestion(vehicle, tree, id);
      expect(question).toContain('exact component and vehicle applicability are unknown');
      expect(question).not.toContain(vehicle.name);
    }
  });

  it('round-trips Unicode and URL delimiters without changing the external host', () => {
    const question = 'Bánh xe & tire? #front https://example.com/?q=override';
    const url = new URL(chatGptQuestionUrl(question));
    expect(url.origin).toBe('https://chatgpt.com');
    expect(url.pathname).toBe('/');
    expect(url.hash).toBe('');
    expect([...url.searchParams]).toEqual([['q', question]]);
  });
});
