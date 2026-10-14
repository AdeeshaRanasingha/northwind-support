import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useSession } from '../../api/session';

export function ProtectedRoute() {
  const session = useSession();
  const location = useLocation();
  return session ? <Outlet /> : <Navigate to="/login" replace state={{
    returnTo: `${location.pathname}${location.search}${location.hash}`,
  }} />;
}
