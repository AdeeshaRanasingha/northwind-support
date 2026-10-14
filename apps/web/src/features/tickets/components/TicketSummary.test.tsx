import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { TicketDetail } from '../../../types/api';
import { TicketSummary } from './TicketSummary';

describe('TicketSummary', () => {
  it('uses the shared SLA indicator in the ticket detail context', () => {
    const ticket: TicketDetail = {
      id: 12,
      reference: 'TCK-0012',
      title: 'Invoice total does not match the order',
      description: 'The invoice total differs from the order total.',
      status: 'InProgress',
      priority: 'High',
      customer: {
        id: 2,
        name: 'Fabrikam Inc',
        tier: 'Premium',
        email: 'support@fabrikam.example',
        phone: null,
      },
      category: { id: 2, name: 'Billing', requiresSpecialist: false },
      assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
      createdAtUtc: '2026-09-24T08:12:00Z',
      updatedAtUtc: '2026-09-25T10:03:00Z',
      dueAtUtc: '2026-09-25T12:12:00Z',
      resolvedAtUtc: null,
      slaStatus: 'AtRisk',
    };

    render(<TicketSummary ticket={ticket} />);

    expect(screen.getByText('At risk')).toHaveClass('badge', 'badge--warning');
    expect(screen.getByText(/Due /)).toBeInTheDocument();
  });
});
