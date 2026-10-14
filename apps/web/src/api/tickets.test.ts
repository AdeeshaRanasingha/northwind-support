import { afterEach, describe, expect, it, vi } from 'vitest';
import { emptyFilters } from '../features/tickets/hooks/useTicketList';
import { ticketsApi } from './tickets';

describe('ticketsApi SLA status filtering', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends slaStatus with existing filters and omits it when cleared', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockImplementation(() =>
        Promise.resolve(new Response(JSON.stringify({ items: [], totalCount: 0 }), { status: 200 })),
      );
    vi.stubGlobal('fetch', fetchMock);

    await ticketsApi.getTickets({
      ...emptyFilters,
      search: 'invoice',
      status: 'InProgress',
      priority: 'High',
      slaStatus: 'AtRisk',
    });

    const firstUrl = String(fetchMock.mock.calls[0][0]);
    expect(firstUrl).toContain('search=invoice');
    expect(firstUrl).toContain('status=InProgress');
    expect(firstUrl).toContain('priority=High');
    expect(firstUrl).toContain('slaStatus=AtRisk');

    await ticketsApi.getTickets({ ...emptyFilters, slaStatus: undefined });

    const clearedUrl = String(fetchMock.mock.calls[1][0]);
    expect(clearedUrl).not.toContain('slaStatus=');
  });

  it('posts escalation data to the ticket escalation endpoint', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify({ ticketId: 12 }), { status: 200 }),
    );
    vi.stubGlobal('fetch', fetchMock);

    await ticketsApi.escalateTicket(12, {
      reason: 'Customer impact increased',
    });

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/tickets/12/escalate',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          reason: 'Customer impact increased',
        }),
      }),
    );
  });
});
