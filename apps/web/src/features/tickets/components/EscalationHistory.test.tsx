import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { Agent, TicketEscalation } from '../../../types/api';
import { EscalationHistory } from './EscalationHistory';

const agents: Agent[] = [
  {
    id: 4,
    fullName: 'Sara Lindqvist',
    email: 'sara@example.test',
    isActive: true,
    maxOpenTickets: 5,
    openTicketCount: 2,
    specializations: [],
  },
];

function escalation(overrides: Partial<TicketEscalation> = {}): TicketEscalation {
  return {
    id: 1,
    ticketId: 12,
    fromPriority: 'Low',
    toPriority: 'Medium',
    fromAgentId: null,
    toAgentId: 4,
    fromDueAtUtc: null,
    toDueAtUtc: '2026-10-14T12:00:00Z',
    reason: 'Customer impact increased',
    escalatedBy: 'Support Lead',
    escalatedAtUtc: '2026-10-14T08:00:00Z',
    ...overrides,
  };
}

const defaultProps = {
  agents,
  isLoading: false,
  error: undefined,
  onRetry: vi.fn(),
};

describe('EscalationHistory', () => {
  it('shows complete audit values newest first', () => {
    render(
      <EscalationHistory
        {...defaultProps}
        records={[
          escalation(),
          escalation({
            id: 2,
            fromPriority: 'Medium',
            toPriority: 'High',
            fromAgentId: 4,
            toAgentId: null,
            fromDueAtUtc: '2026-10-14T12:00:00Z',
            toDueAtUtc: '2026-10-15T08:00:00Z',
            reason: 'Production workflow is blocked',
            escalatedBy: 'Operations Manager',
            escalatedAtUtc: '2026-10-14T09:00:00Z',
          }),
        ]}
      />,
    );

    const entries = screen.getAllByRole('listitem');
    expect(within(entries[0]).getByText('Medium → High')).toBeInTheDocument();
    expect(within(entries[0]).getByText('Sara Lindqvist → Unassigned')).toBeInTheDocument();
    expect(within(entries[0]).getByText('Production workflow is blocked')).toBeInTheDocument();
    expect(within(entries[0]).getByText('Escalated by Operations Manager')).toBeInTheDocument();
    const timestamp = entries[0].querySelector('time');
    expect(timestamp).toHaveAttribute('datetime', '2026-10-14T09:00:00Z');
    expect(timestamp).toHaveTextContent(/Oct 14, 2026.*UTC/);

    expect(within(entries[1]).getByText('Low → Medium')).toBeInTheDocument();
    expect(within(entries[1]).getByText('Unassigned → Sara Lindqvist')).toBeInTheDocument();
    expect(within(entries[1]).getByText(/No due date → .*Oct 14, 2026.*UTC/)).toBeInTheDocument();
  });

  it('falls back to an identifiable agent ID when its name is unavailable', () => {
    render(
      <EscalationHistory
        {...defaultProps}
        records={[escalation({ fromAgentId: 99, toAgentId: null })]}
      />,
    );

    expect(screen.getByText('Agent #99 → Unassigned')).toBeInTheDocument();
  });

  it('shows a friendly empty state', () => {
    render(<EscalationHistory {...defaultProps} records={[]} />);

    expect(screen.getByText('No escalations yet')).toBeInTheDocument();
  });

  it('shows the loading state', () => {
    render(<EscalationHistory {...defaultProps} records={[]} isLoading />);

    expect(screen.getByRole('status')).toHaveTextContent('Loading escalation history...');
  });

  it('shows API errors and supports retry', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(
      <EscalationHistory
        {...defaultProps}
        records={[]}
        error="Unable to load escalation history."
        onRetry={onRetry}
      />,
    );

    expect(screen.getByRole('alert')).toHaveTextContent('Unable to load escalation history.');
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('shows a new record when refreshed history arrives', () => {
    const { rerender } = render(<EscalationHistory {...defaultProps} records={[]} />);
    expect(screen.getByText('No escalations yet')).toBeInTheDocument();

    rerender(<EscalationHistory {...defaultProps} records={[escalation()]} />);

    expect(screen.queryByText('No escalations yet')).not.toBeInTheDocument();
    expect(screen.getByText('Low → Medium')).toBeInTheDocument();
  });
});
