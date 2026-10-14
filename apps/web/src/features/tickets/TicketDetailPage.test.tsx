import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { ticketsApi } from '../../api/tickets';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { EscalateTicketResult, TicketDetail, TicketEscalation } from '../../types/api';
import { useTicket } from './hooks/useTicket';
import { useTicketEscalations } from './hooks/useTicketEscalations';
import { TicketDetailPage } from './TicketDetailPage';

vi.mock('../../api/tickets', () => ({
  ticketsApi: {
    escalateTicket: vi.fn(),
    updateStatus: vi.fn(),
    assignAgent: vi.fn(),
  },
}));

vi.mock('../../hooks/useReferenceData', () => ({ useReferenceData: vi.fn() }));
vi.mock('./hooks/useTicket', () => ({ useTicket: vi.fn() }));
vi.mock('./hooks/useTicketEscalations', () => ({ useTicketEscalations: vi.fn() }));

const escalateTicket = vi.mocked(ticketsApi.escalateTicket);
const mockedUseReferenceData = vi.mocked(useReferenceData);
const mockedUseTicket = vi.mocked(useTicket);
const mockedUseTicketEscalations = vi.mocked(useTicketEscalations);
const reloadTicket = vi.fn().mockResolvedValue(undefined);
const reloadHistory = vi.fn().mockResolvedValue(undefined);

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
  createdAtUtc: '2026-10-14T06:00:00Z',
  updatedAtUtc: '2026-10-14T07:00:00Z',
  dueAtUtc: '2026-10-14T16:00:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
};

const existingHistory: TicketEscalation[] = [
  {
    id: 1,
    ticketId: 12,
    fromPriority: 'Medium',
    toPriority: 'High',
    fromAgentId: 4,
    toAgentId: 4,
    fromDueAtUtc: '2026-10-15T06:00:00Z',
    toDueAtUtc: '2026-10-14T16:00:00Z',
    reason: 'Existing escalation reason',
    escalatedBy: 'Earlier Lead',
    escalatedAtUtc: '2026-10-14T07:00:00Z',
  },
];

const result: EscalateTicketResult = {
  ticketId: 12,
  reference: 'TCK-0012',
  status: 'InProgress',
  priority: 'Critical',
  assignedAgentId: 4,
  dueAtUtc: '2026-10-14T12:00:00Z',
  updatedAtUtc: '2026-10-14T08:00:00Z',
  escalation: {
    id: 2,
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
  assignmentReason: 'Current agent remains eligible.',
};

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/tickets/12']}>
      <Routes>
        <Route path="/tickets/:id" element={<TicketDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

async function submitEscalation(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByRole('textbox', { name: 'Reason' }), 'Customer impact increased');
  await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));
}

describe('TicketDetailPage escalation refresh', () => {
  beforeEach(() => {
    escalateTicket.mockReset();
    reloadTicket.mockClear();
    reloadHistory.mockClear();
    mockedUseTicket.mockReturnValue({
      data: ticket,
      isLoading: false,
      error: undefined,
      reload: reloadTicket,
      setData: vi.fn(),
    });
    mockedUseTicketEscalations.mockReturnValue({
      data: existingHistory,
      isLoading: false,
      error: undefined,
      reload: reloadHistory,
      setData: vi.fn(),
    });
    mockedUseReferenceData.mockReturnValue({
      data: { agents: [], categories: [], customers: [] },
      isLoading: false,
      error: undefined,
      reload: vi.fn(),
      setData: vi.fn(),
    });
  });

  it('refreshes ticket details and history after a successful escalation', async () => {
    const user = userEvent.setup();
    escalateTicket.mockResolvedValue(result);
    renderPage();

    await submitEscalation(user);

    expect(escalateTicket).toHaveBeenCalledOnce();
    expect(reloadTicket).toHaveBeenCalledOnce();
    expect(reloadHistory).toHaveBeenCalledOnce();
  });

  it('keeps ticket details and history unchanged after a conflict', async () => {
    const user = userEvent.setup();
    escalateTicket.mockRejectedValue(
      new ApiError(409, 'The ticket became Critical and cannot be escalated.'),
    );
    renderPage();

    await submitEscalation(user);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The ticket became Critical and cannot be escalated.',
    );
    expect(screen.getByText('Existing escalation reason')).toBeInTheDocument();
    expect(screen.getAllByText('High').length).toBeGreaterThan(0);
    expect(reloadTicket).not.toHaveBeenCalled();
    expect(reloadHistory).not.toHaveBeenCalled();
  });
});
