import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function AdminRoute() {
  const { status, isAdmin } = useAuth();

  if (status === 'loading') {
    return <div className="inline-info">Loading administrative workspace...</div>;
  }

  if (status !== 'authenticated') {
    return <Navigate to="/login" replace />;
  }

  if (!isAdmin) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
