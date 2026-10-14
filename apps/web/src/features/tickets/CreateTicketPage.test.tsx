import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { ticketsApi } from '../../api/tickets';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { TicketDetail } from '../../types/api';
import { CreateTicketPage } from './CreateTicketPage';

vi.mock('../../api/tickets', () => ({
  ticketsApi: { createTicket: vi.fn() },
}));

vi.mock('../../hooks/useReferenceData', () => ({
  useReferenceData: vi.fn(),
}));

const createTicket = vi.mocked(ticketsApi.createTicket);
const mockedUseReferenceData = vi.mocked(useReferenceData);

const created: TicketDetail = {
  id: 44,
  reference: 'TCK-0044',
  title: 'Checkout unavailable',
  description: 'Checkout is unavailable for premium customers.',
  status: 'New',
  priority: 'Critical',
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
  dueAtUtc: '2026-10-14T10:00:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
  triage: {
    priorityReason: 'The selected category requires Critical priority.',
    slaReason: 'Premium customer SLA is 2 hours from creation.',
    assignmentReason: 'Assigned to Sara Lindqvist, the eligible agent with the lowest workload.',
  },
};

function renderPage() {
  return render(
    <MemoryRouter>
      <CreateTicketPage />
    </MemoryRouter>,
  );
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByRole('textbox', { name: 'Title' }), 'Checkout unavailable');
  await user.type(
    screen.getByRole('textbox', { name: 'Description' }),
    'Checkout is unavailable for premium customers.',
  );
  await user.selectOptions(screen.getByRole('combobox', { name: 'Customer' }), '2');
  await user.selectOptions(screen.getByRole('combobox', { name: 'Category' }), '3');
}

describe('CreateTicketPage automatic triage result', () => {
  beforeEach(() => {
    createTicket.mockReset();
    mockedUseReferenceData.mockReturnValue({
      isLoading: false,
      data: {
        customers: [
          {
            id: 2,
            name: 'Fabrikam Inc',
            email: 'support@fabrikam.example',
            phone: null,
            tier: 'Premium',
            openTicketCount: 1,
          },
        ],
        categories: [
          {
            id: 3,
            name: 'Orders',
            isActive: true,
            requiresSpecialist: false,
            forcesCriticalPriority: true,
          },
        ],
        agents: [],
      },
      error: undefined,
      reload: vi.fn(),
      setData: vi.fn(),
    });
  });

  it('keeps the successful creation result visible with backend triage data', async () => {
    const user = userEvent.setup();
    createTicket.mockResolvedValue(created);
    renderPage();
    await fillValidForm(user);

    await user.click(screen.getByRole('button', { name: 'Raise ticket' }));

    expect(await screen.findByRole('heading', { name: 'Ticket created' })).toBeInTheDocument();
    expect(screen.getByText('The selected category requires Critical priority.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Raise ticket' })).not.toBeInTheDocument();
  });

  it('does not display a false success result when creation fails', async () => {
    const user = userEvent.setup();
    createTicket.mockRejectedValue(new ApiError(500, 'Ticket creation failed.'));
    renderPage();
    await fillValidForm(user);

    await user.click(screen.getByRole('button', { name: 'Raise ticket' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Ticket creation failed.');
    expect(screen.queryByRole('heading', { name: 'Ticket created' })).not.toBeInTheDocument();
  });

  it('preserves existing client validation and does not submit invalid input', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole('button', { name: 'Raise ticket' }));

    expect(screen.getByText('Give the ticket a title of at least 5 characters.')).toBeInTheDocument();
    expect(createTicket).not.toHaveBeenCalled();
  });
});
