import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  slaStatus: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [requestFilters, setRequestFilters] = useState<TicketFilters>(emptyFilters);
  const [state, setState] = useState<State>({ isLoading: true });
  const latestRequestId = useRef(0);

  const load = useCallback(async () => {
    const requestId = ++latestRequestId.current;
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await ticketsApi.getTickets(requestFilters);

      if (requestId === latestRequestId.current) {
        setState({ isLoading: false, data, error: undefined });
      }
    } catch (error) {
      if (requestId === latestRequestId.current) {
        const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
        setState((current) => ({ ...current, isLoading: false, error: message }));
      }
    }
  }, [requestFilters]);

  useEffect(() => {
    let cancelled = false;

    queueMicrotask(() => {
      if (!cancelled) {
        void load();
      }
    });

    return () => {
      cancelled = true;
    };
  }, [load]);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setRequestFilters((current) =>
        current.search === filters.search
          ? current
          : { ...current, search: filters.search, page: 1 },
      );
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [filters.search]);

  const updateFilters = (patch: Partial<TicketFilters>) => {
    const page = patch.page ?? 1;

    setFilters((current) => ({ ...current, ...patch, page }));

    const immediatePatch = { ...patch };
    delete immediatePatch.search;

    if (Object.keys(immediatePatch).length > 0) {
      setRequestFilters((current) => ({ ...current, ...immediatePatch, page }));
    }
  };

  const resetFilters = () => {
    setFilters(emptyFilters);
    setRequestFilters(emptyFilters);
  };

  return { filters, updateFilters, resetFilters, reload: load, ...state };
}
