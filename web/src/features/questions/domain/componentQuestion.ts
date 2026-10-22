import type { Vehicle } from '../../../domain/catalog';
import { itemLabel, type ItemTree } from '../../exploration/domain/items';

export function componentQuestion(vehicle: Vehicle, tree: ItemTree, itemId: string): string {
  const item = tree.items.find(candidate => candidate.id === itemId);
  if (!item || item.id === 'vehicle') {
    return 'Explain the purpose and relationships of a car component in an educational diagram. The exact component and vehicle applicability are unknown. Do not provide repair instructions, diagnosis or safe-to-drive decisions. State uncertainty and suggest applicable sources.';
  }
  const parent = tree.items.find(candidate => candidate.id === item.parentId);
  const relationship = parent ? ` It is shown within ${itemLabel(tree, parent)}.` : '';
  return `Explain the purpose of ${itemLabel(tree, item)} and its relationships in a car diagram.${relationship} The public workshop catalog depicts ${vehicle.year} ${vehicle.name}; this representative schematic does not establish exact fitment or the identity of a real vehicle. Keep the answer educational, not repair instructions, diagnosis or a safe-to-drive decision. State uncertainty and suggest applicable sources.`;
}

export function chatGptQuestionUrl(question: string): string {
  const url = new URL('https://chatgpt.com/');
  url.searchParams.set('q', question);
  return url.href;
}
