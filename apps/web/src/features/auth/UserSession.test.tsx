import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { request } from '../../api/client';
import { clearSession, getSession, saveSession } from '../../api/session';
import { ProtectedRoute } from './ProtectedRoute';
import { UserSession } from './UserSession';

const fetchMock = vi.fn<typeof fetch>();
beforeEach(() => {
  clearSession();
  fetchMock.mockReset();
  fetchMock.mockResolvedValue(new Response(JSON.stringify({ id: 1, fullName: 'Alex Turner', email: 'alex@example.test' }), { status: 200 }));
  vi.stubGlobal('fetch', fetchMock);
  saveSession({ accessToken: 'test-token', expiresAtUtc: '2030-01-01T00:00:00Z' });
});
afterEach(() => { cleanup(); clearSession(); vi.unstubAllGlobals(); });

function LoginDestination() {
  const location = useLocation();
  return <p>Login {JSON.stringify(location.state)}</p>;
}
function renderSession() {
  render(<MemoryRouter initialEntries={['/tickets?search=invoice#results']}>
    <UserSession />
    <Routes>
      <Route path="/login" element={<LoginDestination />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/tickets" element={<p>Protected tickets</p>} />
      </Route>
    </Routes>
  </MemoryRouter>);
}

it('shows authenticated agent and logout clears storage, redirects and removes Bearer headers', async () => {
  renderSession();
  expect(await screen.findByText('Signed in as Alex Turner')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Logout' }));
  expect(screen.getByText(/^Login/)).toBeInTheDocument();
  expect(screen.queryByText('Protected tickets')).not.toBeInTheDocument();
  expect(getSession()).toBeNull();
  fetchMock.mockResolvedValueOnce(new Response('{}', { status: 200 }));
  await request('/tickets');
  expect(new Headers(fetchMock.mock.calls.at(-1)?.[1]?.headers).has('Authorization')).toBe(false);
});

it('API 401 redirects through the existing guard and preserves the protected destination', async () => {
  renderSession();
  await screen.findByText('Signed in as Alex Turner');
  fetchMock.mockResolvedValueOnce(new Response('', { status: 401 }));
  await act(async () => {
    await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });
  });
  expect(screen.getByText(/^Login/)).toHaveTextContent('/tickets?search=invoice#results');
  expect(screen.queryByText('Protected tickets')).not.toBeInTheDocument();
});

it('handles missing agent information while retaining logout', async () => {
  fetchMock.mockRejectedValue(new TypeError('Network unavailable'));
  renderSession();
  expect(await screen.findByText('Agent information unavailable')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Logout' })).toBeEnabled();
});
