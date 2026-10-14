import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { EscalateTicketResult, TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: { escalateTicket: vi.fn() },
}));

const escalateTicket = vi.mocked(ticketsApi.escalateTicket);

const escalationResult: EscalateTicketResult = {
  ticketId: 12,
  reference: 'TCK-0012',
  status: 'InProgress',
  priority: 'Critical',
  assignedAgentId: 4,
  dueAtUtc: '2026-10-14T12:00:00Z',
  updatedAtUtc: '2026-10-14T08:00:00Z',
  assignmentReason: 'Current agent remains eligible.',
  escalation: {
    id: 3,
    ticketId: 12,
    fromPriority: 'High',
    toPriority: 'Critical',
    fromAgentId: 4,
    toAgentId: 4,
    fromDueAtUtc: '2026-10-14T16:00:00Z',
    toDueAtUtc: '2026-10-14T12:00:00Z',
    reason: 'Customer impact increased',
    escalatedBy: 'Support Lead',
    escalatedAtUtc: '2026-10-14T08:00:00Z',
  },
};

function ticket(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
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
    ...overrides,
  };
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByRole('textbox', { name: 'Reason' }), 'Customer impact increased');
  await user.type(screen.getByRole('textbox', { name: 'Escalated by' }), 'Support Lead');
}

describe('EscalationPanel', () => {
  beforeEach(() => {
    escalateTicket.mockReset();
  });

  it('blocks a reason shorter than five trimmed characters', async () => {
    const user = userEvent.setup();
    render(<EscalationPanel ticket={ticket()} onEscalated={vi.fn()} />);

    await user.type(screen.getByRole('textbox', { name: 'Reason' }), ' 123 ');
    await user.type(screen.getByRole('textbox', { name: 'Escalated by' }), 'Support Lead');
    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(screen.getByText('Reason must contain 5 to 500 characters.')).toBeInTheDocument();
    expect(escalateTicket).not.toHaveBeenCalled();
  });

  it('blocks a reason longer than 500 trimmed characters', async () => {
    const user = userEvent.setup();
    render(<EscalationPanel ticket={ticket()} onEscalated={vi.fn()} />);

    fireEvent.change(screen.getByRole('textbox', { name: 'Reason' }), {
      target: { value: 'x'.repeat(501) },
    });
    await user.type(screen.getByRole('textbox', { name: 'Escalated by' }), 'Support Lead');
    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(screen.getByText('Reason must contain 5 to 500 characters.')).toBeInTheDocument();
    expect(escalateTicket).not.toHaveBeenCalled();
  });

  it('blocks a blank actor', async () => {
    const user = userEvent.setup();
    render(<EscalationPanel ticket={ticket()} onEscalated={vi.fn()} />);

    await user.type(screen.getByRole('textbox', { name: 'Reason' }), 'Customer impact increased');
    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(screen.getByText(/Escalated by is required/)).toBeInTheDocument();
    expect(escalateTicket).not.toHaveBeenCalled();
  });

  it('submits trimmed values and refreshes data after success', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn().mockResolvedValue(undefined);
    escalateTicket.mockResolvedValue(escalationResult);
    render(<EscalationPanel ticket={ticket()} onEscalated={onEscalated} />);

    await user.type(screen.getByRole('textbox', { name: 'Reason' }), '  Customer impact increased  ');
    await user.type(screen.getByRole('textbox', { name: 'Escalated by' }), '  Support Lead  ');
    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(escalateTicket).toHaveBeenCalledWith(12, {
      reason: 'Customer impact increased',
      escalatedBy: 'Support Lead',
    });
    expect(onEscalated).toHaveBeenCalledOnce();
  });

  it('prevents duplicate submissions while the request is pending', async () => {
    const user = userEvent.setup();
    let resolveRequest: (() => void) | undefined;
    escalateTicket.mockImplementation(
      () => new Promise((resolve) => (resolveRequest = () => resolve(escalationResult))),
    );
    render(<EscalationPanel ticket={ticket()} onEscalated={vi.fn()} />);
    await fillValidForm(user);

    const submit = screen.getByRole('button', { name: 'Escalate ticket' });
    await user.click(submit);

    expect(screen.getByRole('button', { name: 'Escalating...' })).toBeDisabled();
    await user.click(screen.getByRole('button', { name: 'Escalating...' }));
    expect(escalateTicket).toHaveBeenCalledOnce();

    resolveRequest?.();
  });

  it('shows a conflict without refreshing or changing the displayed ticket', async () => {
    const user = userEvent.setup();
    const onEscalated = vi.fn();
    escalateTicket.mockRejectedValue(
      new ApiError(409, 'The ticket became Critical and cannot be escalated.'),
    );
    render(<EscalationPanel ticket={ticket()} onEscalated={onEscalated} />);
    await fillValidForm(user);

    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The ticket became Critical and cannot be escalated.',
    );
    expect(onEscalated).not.toHaveBeenCalled();
    expect(screen.getByRole('textbox', { name: 'Reason' })).toHaveValue(
      'Customer impact increased',
    );
  });

  it.each([
    ['Critical', 'InProgress'],
    ['High', 'Resolved'],
    ['High', 'Closed'],
  ] as const)('does not offer submission for %s/%s tickets', (priority, status) => {
    render(<EscalationPanel ticket={ticket({ priority, status })} onEscalated={vi.fn()} />);

    expect(screen.queryByRole('button', { name: 'Escalate ticket' })).not.toBeInTheDocument();
    expect(screen.getByText(/cannot be escalated/)).toBeInTheDocument();
  });
});
