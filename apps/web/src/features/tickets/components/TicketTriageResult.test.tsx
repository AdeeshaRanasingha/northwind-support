import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { TicketDetail } from '../../../types/api';
import { TicketTriageResult } from './TicketTriageResult';

function createdTicket(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: 44,
    reference: 'TCK-0044',
    title: 'Checkout unavailable',
    description: 'Checkout is unavailable for premium customers.',
    status: 'New',
    priority: 'High',
    customer: {
      id: 2,
      name: 'Fabrikam Inc',
      tier: 'Premium',
      email: 'support@fabrikam.example',
      phone: null,
    },
    category: { id: 3, name: 'Orders', requiresSpecialist: false },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-10-14T08:00:00Z',
    updatedAtUtc: '2026-10-14T08:00:00Z',
    dueAtUtc: '2026-10-14T12:00:00Z',
    resolvedAtUtc: null,
    slaStatus: 'WithinSla',
    triage: {
      priorityReason: 'The requested High priority was applied.',
      slaReason: 'Premium customer SLA is 4 hours from creation.',
      assignmentReason: 'Assigned to Sara Lindqvist, the eligible agent with the lowest workload.',
    },
    ...overrides,
  };
}

describe('TicketTriageResult', () => {
  it('displays the values and explanations returned by automatic triage', () => {
    render(<TicketTriageResult ticket={createdTicket()} />);

    expect(screen.getByText('High')).toBeInTheDocument();
    expect(screen.getByText(/Oct 14, 2026.*UTC/)).toBeInTheDocument();
    expect(screen.getByText('Sara Lindqvist')).toBeInTheDocument();
    expect(screen.getByText('Within SLA')).toBeInTheDocument();
    expect(screen.getByText('The requested High priority was applied.')).toBeInTheDocument();
    expect(screen.getByText('Premium customer SLA is 4 hours from creation.')).toBeInTheDocument();
    expect(
      screen.getByText('Assigned to Sara Lindqvist, the eligible agent with the lowest workload.'),
    ).toBeInTheDocument();
  });

  it('shows the backend reason when triage leaves the ticket unassigned', () => {
    render(
      <TicketTriageResult
        ticket={createdTicket({
          assignedAgent: null,
          triage: {
            priorityReason: 'No priority was requested, so Medium priority was applied.',
            slaReason: 'Standard customer SLA is 24 hours from creation.',
            assignmentReason: 'Unassigned because no active agent has available capacity.',
          },
        })}
      />,
    );

    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(
      screen.getByText('Unassigned because no active agent has available capacity.'),
    ).toBeInTheDocument();
  });
});
