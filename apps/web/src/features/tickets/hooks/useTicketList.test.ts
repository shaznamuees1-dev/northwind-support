import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

let fetchMock: ReturnType<typeof vi.spyOn>;

function requestParams(callIndex: number): URLSearchParams {
  const input = fetchMock.mock.calls[callIndex]?.[0];
  return new URL(String(input), 'http://localhost').searchParams;
}

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    fetchMock = vi.spyOn(globalThis, 'fetch');
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify(page([ticketListItem()])), {
        headers: { 'Content-Type': 'application/json' },
      }),
    );
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('requests the selected status immediately', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);

    await act(async () => {
      result.current.updateFilters({ status: 'InProgress' });
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toMatch(/^\/api\/tickets\?/);
    expect(requestParams(1).get('status')).toBe('InProgress');
  });

  it('debounces search and sends the settled text with the current filters', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
      await Promise.resolve();
      await Promise.resolve();
    });

    await act(async () => {
      result.current.updateFilters({ status: 'InProgress' });
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);

    await act(async () => {
      result.current.updateFilters({ search: 'i' });
      result.current.updateFilters({ search: 'in' });
      result.current.updateFilters({ search: 'invoice' });
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(299);
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(requestParams(2).get('search')).toBe('invoice');
    expect(requestParams(2).get('status')).toBe('InProgress');

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });
});
