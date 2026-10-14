import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import type { TicketFilters } from '../../../types/api';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

const immediateFilterChanges: Array<[string, Partial<TicketFilters>]> = [
  ['status', { status: 'InProgress' }],
  ['priority', { priority: 'High' }],
  ['SLA status', { slaStatus: 'AtRisk' }],
  ['category', { categoryId: 3 }],
  ['customer', { customerId: 2 }],
  ['assigned agent', { assignedAgentId: 4 }],
  ['unassigned only', { unassignedOnly: true }],
  ['sort field', { sortBy: 'priority' }],
  ['sort direction', { sortDirection: 'asc' }],
  ['page', { page: 2 }],
  ['page size', { pageSize: 50 }],
];

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });

  it('requests tickets when only the status filter changes', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ status: 'InProgress' });
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'InProgress', page: 1 }),
    );
  });

  it.each(immediateFilterChanges)(
    'requests immediately when the %s filter changes',
    async (_name, patch) => {
      const { result } = renderHook(() => useTicketList());

      await act(async () => undefined);
      expect(getTickets).toHaveBeenCalledTimes(1);

      await act(async () => {
        result.current.updateFilters(patch);
      });

      expect(getTickets).toHaveBeenCalledTimes(2);
      expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining(patch));
    },
  );

  it('preserves all active filters in later requests', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => undefined);

    await act(async () => {
      result.current.updateFilters({ status: 'InProgress' });
    });
    await act(async () => {
      result.current.updateFilters({ priority: 'High' });
    });
    await act(async () => {
      result.current.updateFilters({ categoryId: 3 });
    });
    await act(async () => {
      result.current.updateFilters({ slaStatus: 'Breached' });
    });

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({
        status: 'InProgress',
        priority: 'High',
        categoryId: 3,
        slaStatus: 'Breached',
        page: 1,
      }),
    );
  });

  it('clears the SLA status and resets pagination without affecting search debouncing', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => undefined);
    await act(async () => {
      result.current.updateFilters({ page: 3 });
      result.current.updateFilters({ slaStatus: 'AtRisk' });
    });

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ slaStatus: 'AtRisk', page: 1 }),
    );

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    await act(async () => {
      result.current.updateFilters({ slaStatus: undefined });
    });

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ slaStatus: undefined, search: '', page: 1 }),
    );

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ slaStatus: undefined, search: 'invoice', page: 1 }),
    );
  });

  it('requests non-search filters immediately while a search debounce is pending', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => undefined);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      result.current.updateFilters({ status: 'InProgress' });
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: '', status: 'InProgress' }),
    );

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(3);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', status: 'InProgress' }),
    );
  });

  it('debounces rapid searching, then handles clearing and a later status change', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => undefined);

    act(() => {
      result.current.updateFilters({ search: 'i' });
      result.current.updateFilters({ search: 'in' });
      result.current.updateFilters({ search: 'invoice' });
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(299);
    });
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });
    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice' }),
    );

    act(() => {
      result.current.updateFilters({ search: '' });
    });
    expect(getTickets).toHaveBeenCalledTimes(2);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });
    expect(getTickets).toHaveBeenCalledTimes(3);
    expect(getTickets).toHaveBeenLastCalledWith(expect.objectContaining({ search: '' }));

    await act(async () => {
      result.current.updateFilters({ status: 'Open' });
    });
    expect(getTickets).toHaveBeenCalledTimes(4);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: '', status: 'Open' }),
    );
  });
});
