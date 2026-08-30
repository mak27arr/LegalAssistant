import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../features/auth/AuthContext';
import { getAdminRoles, getAdminUsers, updateAdminUserRoles } from '../shared/api/client';
import type { AdminRoleResponse, AdminUserPageResponse, AdminUserResponse } from '../shared/types/api';

type DraftState = Record<string, string[]>;
type StatusFilter = 'all' | 'active' | 'blocked';
type SortOption = 'last_login_desc' | 'last_login_asc' | 'name_asc' | 'name_desc' | 'email_asc' | 'email_desc' | 'created_desc' | 'created_asc';

export function AdminUsersPage() {
  const { user } = useAuth();
  const [userPage, setUserPage] = useState<AdminUserPageResponse | null>(null);
  const [roles, setRoles] = useState<AdminRoleResponse[]>([]);
  const [drafts, setDrafts] = useState<DraftState>({});
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [sort, setSort] = useState<SortOption>('last_login_desc');
  const [pageSize, setPageSize] = useState(10);
  const [page, setPage] = useState(1);
  const [savingUserId, setSavingUserId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    void loadData(page);
  }, [page, pageSize, search, statusFilter, sort]);

  async function loadData(targetPage: number) {
    setIsLoading(true);
    setError(null);

    try {
      const [loadedUsers, loadedRoles] = await Promise.all([
        getAdminUsers({
          search,
          status: statusFilter === 'all' ? undefined : statusFilter,
          sort,
          page: targetPage,
          pageSize
        }),
        getAdminRoles()
      ]);
      setUserPage(loadedUsers);
      setRoles(loadedRoles);
      setDrafts((current) => ({
        ...current,
        ...Object.fromEntries(loadedUsers.items.map((loadedUser) => [loadedUser.id, [...loadedUser.roles]]))
      }));
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to load user administration data.');
    } finally {
      setIsLoading(false);
    }
  }

  function toggleRole(userId: string, roleName: string) {
    setDrafts((current) => {
      const currentRoles = current[userId] ?? [];
      const hasRole = currentRoles.includes(roleName);
      const nextRoles = hasRole
        ? currentRoles.filter((candidate) => candidate !== roleName)
        : [...currentRoles, roleName];

      return {
        ...current,
        [userId]: nextRoles
      };
    });
  }

  async function saveRoles(targetUser: AdminUserResponse) {
    setSavingUserId(targetUser.id);
    setError(null);

    try {
      const updated = await updateAdminUserRoles(targetUser.id, drafts[targetUser.id] ?? []);
      setUserPage((current) => current
        ? {
            ...current,
            items: current.items.map((candidate) => candidate.id === updated.id ? updated : candidate)
          }
        : current);
      setDrafts((current) => ({
        ...current,
        [updated.id]: [...updated.roles]
      }));
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to update user roles.');
    } finally {
      setSavingUserId(null);
    }
  }

  function hasDraftChanges(targetUser: AdminUserResponse) {
    const draftRoles = [...(drafts[targetUser.id] ?? [])].sort().join('|');
    const currentRoles = [...targetUser.roles].sort().join('|');
    return draftRoles !== currentRoles;
  }

  function applyFilters() {
    setPage(1);
    setSearch(searchInput.trim());
  }

  function resetFilters() {
    setSearchInput('');
    setSearch('');
    setStatusFilter('all');
    setSort('last_login_desc');
    setPageSize(10);
    setPage(1);
  }

  const users = userPage?.items ?? [];

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Admin Only</p>
          <h2>User management</h2>
          <p>Review signed-in users and assign the roles that control access to the operator console.</p>
        </div>
        <span className="code-chip">GET /api/admin/users</span>
      </div>

      <div className="button-row">
        <Link className="button-secondary" to="/">
          Back to intake
        </Link>
        <button className="button-secondary" type="button" onClick={() => void loadData(page)}>
          {isLoading ? 'Refreshing...' : 'Refresh users'}
        </button>
      </div>

      <div className="filters-grid compact-card">
        <div className="field-group">
          <label htmlFor="user-search">Search</label>
          <input
            id="user-search"
            placeholder="Name or email"
            type="text"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                applyFilters();
              }
            }}
          />
        </div>
        <div className="field-group">
          <label htmlFor="user-status">Status</label>
          <select id="user-status" value={statusFilter} onChange={(event) => {
            setPage(1);
            setStatusFilter(event.target.value as StatusFilter);
          }}>
            <option value="all">All</option>
            <option value="active">Active</option>
            <option value="blocked">Blocked</option>
          </select>
        </div>
        <div className="field-group">
          <label htmlFor="user-sort">Sort</label>
          <select id="user-sort" value={sort} onChange={(event) => {
            setPage(1);
            setSort(event.target.value as SortOption);
          }}>
            <option value="last_login_desc">Last login: newest</option>
            <option value="last_login_asc">Last login: oldest</option>
            <option value="name_asc">Name: A-Z</option>
            <option value="name_desc">Name: Z-A</option>
            <option value="email_asc">Email: A-Z</option>
            <option value="email_desc">Email: Z-A</option>
            <option value="created_desc">Created: newest</option>
            <option value="created_asc">Created: oldest</option>
          </select>
        </div>
        <div className="field-group">
          <label htmlFor="user-page-size">Page size</label>
          <select id="user-page-size" value={pageSize} onChange={(event) => {
            setPage(1);
            setPageSize(Number(event.target.value));
          }}>
            <option value={10}>10</option>
            <option value={20}>20</option>
            <option value={50}>50</option>
          </select>
        </div>
      </div>

      <div className="button-row compact-card">
        <button className="button-primary" type="button" onClick={applyFilters}>
          Apply filters
        </button>
        <button className="button-secondary" type="button" onClick={resetFilters}>
          Reset
        </button>
        {userPage ? (
          <span className="page-indicator">
            Showing {users.length} of {userPage.totalItems} users
          </span>
        ) : null}
      </div>

      {error ? <div className="inline-error">{error}</div> : null}

      {isLoading ? (
        <div className="inline-info">Loading users and roles...</div>
      ) : users.length === 0 ? (
        <div className="inline-info">No users have signed in yet.</div>
      ) : (
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>User</th>
                <th>Current roles</th>
                <th>Edit roles</th>
                <th>Last login</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((managedUser) => {
                const draftRoles = drafts[managedUser.id] ?? managedUser.roles;
                const isSelf = user?.id === managedUser.id;

                return (
                  <tr key={managedUser.id}>
                    <td>
                      <Link className="entity-link" to={`/admin/users/${managedUser.id}`}>
                        <strong>{managedUser.fullName}</strong>
                      </Link>
                      <div className="table-subtitle">{managedUser.email}</div>
                      <div className="table-subtitle">
                        {managedUser.isActive ? 'Active account' : 'Blocked account'}
                      </div>
                    </td>
                    <td>
                      <div className="role-pill-list">
                        {managedUser.roles.map((role) => (
                          <span className="role-pill" key={`${managedUser.id}-current-${role}`}>
                            {role}
                          </span>
                        ))}
                      </div>
                    </td>
                    <td>
                      <div className="role-editor">
                        {roles.map((role) => {
                          const checked = draftRoles.includes(role.name);
                          const disableToggle = isSelf && role.name === 'Admin';

                          return (
                            <label className="role-checkbox" key={`${managedUser.id}-${role.id}`}>
                              <input
                                checked={checked}
                                disabled={disableToggle || savingUserId === managedUser.id}
                                type="checkbox"
                                onChange={() => toggleRole(managedUser.id, role.name)}
                              />
                              <span>{role.name}</span>
                            </label>
                          );
                        })}
                      </div>
                    </td>
                    <td>{managedUser.lastLoginAt ? new Date(managedUser.lastLoginAt).toLocaleString() : 'Never'}</td>
                    <td>
                      <div className="button-row">
                        <Link className="button-secondary button-inline" to={`/admin/users/${managedUser.id}`}>
                          Open details
                        </Link>
                        <button
                          className="button-primary"
                          disabled={!hasDraftChanges(managedUser) || savingUserId === managedUser.id}
                          type="button"
                          onClick={() => void saveRoles(managedUser)}
                        >
                          {savingUserId === managedUser.id ? 'Saving...' : 'Save roles'}
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          {userPage ? (
            <div className="button-row compact-card">
              <button
                className="button-secondary"
                disabled={!userPage.hasPreviousPage || isLoading}
                type="button"
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                Previous
              </button>
              <span className="page-indicator">
                Page {userPage.page} of {userPage.totalPages}
              </span>
              <button
                className="button-secondary"
                disabled={!userPage.hasNextPage || isLoading}
                type="button"
                onClick={() => setPage((current) => current + 1)}
              >
                Next
              </button>
            </div>
          ) : null}
        </div>
      )}
    </section>
  );
}
