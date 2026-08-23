import { NavLink, Route, Routes } from 'react-router-dom';
import { AskPage } from './pages/AskPage';
import { DocumentsPage } from './pages/DocumentsPage';

const navLinkClassName = ({ isActive }: { isActive: boolean }) =>
  isActive ? 'nav-link nav-link-active' : 'nav-link';

export default function App() {
  return (
    <div className="app-shell">
      <header className="hero">
        <div className="hero-copy">
          <p className="eyebrow">Legal Assistant</p>
          <h1>Operator console for ingestion and grounded answers.</h1>
          <p className="hero-text">
            The first frontend iteration stays inside the current backend contract: two focused workflows,
            zero unapproved API changes.
          </p>
        </div>
        <nav className="nav-card" aria-label="Primary navigation">
          <NavLink className={navLinkClassName} to="/">
            Documents
          </NavLink>
          <NavLink className={navLinkClassName} to="/ask">
            Ask
          </NavLink>
        </nav>
      </header>

      <main className="page-frame">
        <Routes>
          <Route path="/" element={<DocumentsPage />} />
          <Route path="/ask" element={<AskPage />} />
        </Routes>
      </main>
    </div>
  );
}
