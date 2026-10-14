import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

describe('SlaIndicator', () => {
  const cases: ReadonlyArray<[SlaStatus, string, string]> = [
    ['WithinSla', 'Within SLA', 'badge--info'],
    ['AtRisk', 'At risk', 'badge--warning'],
    ['Breached', 'Breached', 'badge--danger'],
    ['Met', 'Met', 'badge--success'],
    ['NotApplicable', 'No SLA', 'badge--neutral'],
  ];

  it.each(cases)('renders %s as a distinct readable indicator', (status, label, toneClass) => {
    render(<SlaIndicator status={status} />);

    expect(screen.getByText(label)).toHaveClass('badge', toneClass);
  });
});
