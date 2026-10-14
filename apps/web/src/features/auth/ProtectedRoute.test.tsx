import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { clearSession, sessionStorageKey } from '../../api/session';
import { LoginPage } from './LoginPage';
import { ProtectedRoute } from './ProtectedRoute';
import { safeReturnPath } from './returnPath';

vi.mock('../../api/auth', () => ({ authApi: { login: vi.fn() } }));
const session = { accessToken: 'test-token', expiresAtUtc: '2030-01-01T00:00:00Z' };
beforeEach(() => { clearSession(); vi.mocked(authApi.login).mockReset(); });
afterEach(() => { cleanup(); clearSession(); });

function Location() {
  const location = useLocation();
  return <p data-testid="location">{location.pathname}{location.search}{location.hash}</p>;
}

function renderRoutes(path: string, state?: unknown) {
  render(<MemoryRouter initialEntries={[{ pathname: path.split(/[?#]/)[0],
    search: path.includes('?') ? `?${path.split('?')[1].split('#')[0]}` : '',
    hash: path.includes('#') ? `#${path.split('#')[1]}` : '', state }]}>
    <Location />
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/tickets" element={<p>Ticket list</p>} />
        <Route path="/tickets/:id" element={<p>Protected ticket</p>} />
        <Route path="*" element={<p>Protected fallback</p>} />
      </Route>
    </Routes>
  </MemoryRouter>);
}

it('redirects without rendering protected content and returns to path, query and hash after login', async () => {
  vi.mocked(authApi.login).mockResolvedValue({ ...session, agentId: 1, fullName: 'Alex', email: 'agent@example.test' });
  renderRoutes('/tickets/42?tab=history#latest');
  expect(screen.getByTestId('location')).toHaveTextContent('/login');
  expect(screen.queryByText('Protected ticket')).not.toBeInTheDocument();
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'agent@example.test' } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'test-password' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByText('Protected ticket')).toBeInTheDocument();
  expect(screen.getByTestId('location')).toHaveTextContent('/tickets/42?tab=history#latest');
});

it('restores a valid stored session on mount without redirecting', () => {
  sessionStorage.setItem(sessionStorageKey, JSON.stringify(session));
  renderRoutes('/tickets/42');
  expect(screen.getByText('Protected ticket')).toBeInTheDocument();
  expect(screen.getByTestId('location')).toHaveTextContent('/tickets/42');
});

it('rejects expired storage before rendering a protected page', () => {
  sessionStorage.setItem(sessionStorageKey, JSON.stringify({ ...session, expiresAtUtc: '2000-01-01T00:00:00Z' }));
  renderRoutes('/tickets/42');
  expect(screen.getByTestId('location')).toHaveTextContent('/login');
  expect(screen.queryByText('Protected ticket')).not.toBeInTheDocument();
});

it.each([undefined, { returnTo: 'https://evil.example' }, { returnTo: '//evil.example' },
  { returnTo: '/login' }, { returnTo: '/tickets/../login' }, { returnTo: '/\\evil.example' },
  { returnTo: '/%2f%2fevil.example' }])('authenticated login rejects unsafe/loop destinations: %j', (state) => {
  sessionStorage.setItem(sessionStorageKey, JSON.stringify(session));
  renderRoutes('/login', state);
  expect(screen.getByText('Ticket list')).toBeInTheDocument();
  expect(screen.getByTestId('location')).toHaveTextContent('/tickets');
});

it('authenticated login returns directly to a safe requested destination', () => {
  sessionStorage.setItem(sessionStorageKey, JSON.stringify(session));
  renderRoutes('/login', { returnTo: '/tickets/42?tab=history#latest' });
  expect(screen.getByText('Protected ticket')).toBeInTheDocument();
  expect(screen.getByTestId('location')).toHaveTextContent('/tickets/42?tab=history#latest');
});

it('retains encoded query values without permitting encoded path redirects', () => {
  expect(safeReturnPath({ returnTo: '/tickets?search=invoice%20total#results' }))
    .toBe('/tickets?search=invoice%20total#results');
});
