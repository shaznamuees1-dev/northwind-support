import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
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
  const [debouncedSearch, setDebouncedSearch] = useState(filters.search);
  const [state, setState] = useState<State>({ isLoading: true });

  const load = useCallback(async (nextFilters: TicketFilters) => {
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await ticketsApi.getTickets(nextFilters);
      setState({ isLoading: false, data, error: undefined });
    } catch (error) {
      const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
      setState((current) => ({ ...current, isLoading: false, error: message }));
    }
  }, []);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setDebouncedSearch(filters.search);
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [filters.search]);

  const lastRequestedFilters = useRef<TicketFilters | undefined>(undefined);
  useEffect(() => {
    const requestFilters = { ...filters, search: debouncedSearch };
    const previous = lastRequestedFilters.current;
    if (
      previous &&
      JSON.stringify(requestFilters) === JSON.stringify(previous)
    ) {
      return;
    }

    lastRequestedFilters.current = requestFilters;
    void load(requestFilters);
  }, [debouncedSearch, filters, load]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => setFilters(emptyFilters);

  return { filters, updateFilters, resetFilters, reload: () => void load(filters), ...state };
}
