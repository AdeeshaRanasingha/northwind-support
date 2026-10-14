import { Link, NavLink, Outlet } from 'react-router-dom';
import { UserSession } from './features/auth/UserSession';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>
        <UserSession />
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
