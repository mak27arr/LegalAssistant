import { NavLink, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './features/auth/ProtectedRoute';
import { useAuth } from './features/auth/AuthContext';
import { AskPage } from './pages/AskPage';
import { AuthCallbackPage } from './pages/AuthCallbackPage';
import { DocumentDetailsPage } from './pages/DocumentDetailsPage';
import { DocumentChunksPage } from './pages/DocumentChunksPage';
import { DocumentsDatabasePage } from './pages/DocumentsDatabasePage';
import { DocumentsPage } from './pages/DocumentsPage';
import { LoginPage } from './pages/LoginPage';

const navLinkClassName = ({ isActive }: { isActive: boolean }) =>
  isActive ? 'nav-link nav-link-active' : 'nav-link';

export default function App() {
  const { status, user, logout } = useAuth();

  return (
    <div className="app-shell">
      <header className="hero">
        <div className="hero-copy">
          <p className="eyebrow">Legal Assistant</p>
          <h1>Operator console</h1>
          {status === 'authenticated' && user ? <p>{user.fullName} · {user.roles.join(', ')}</p> : null}
        </div>
        <nav className="nav-card" aria-label="Primary navigation">
          {status === 'authenticated' ? (
            <>
              <NavLink className={navLinkClassName} to="/">
                Intake
              </NavLink>
              <NavLink className={navLinkClassName} to="/documents">
                Documents
              </NavLink>
              <NavLink className={navLinkClassName} to="/ask">
                Ask
              </NavLink>
              <button className="button-secondary" type="button" onClick={() => void logout()}>
                Sign out
              </button>
            </>
          ) : (
            <NavLink className={navLinkClassName} to="/login">
              Sign in
            </NavLink>
          )}
        </nav>
      </header>

      <main className="page-frame">
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/auth/callback" element={<AuthCallbackPage />} />
          <Route element={<ProtectedRoute />}>
            <Route path="/" element={<DocumentsPage />} />
            <Route path="/documents" element={<DocumentsDatabasePage />} />
            <Route path="/documents/:documentId" element={<DocumentDetailsPage />} />
            <Route path="/documents/:documentId/chunks" element={<DocumentChunksPage />} />
            <Route path="/ask" element={<AskPage />} />
          </Route>
        </Routes>
      </main>
    </div>
  );
}
