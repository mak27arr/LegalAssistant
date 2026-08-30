import type {
  AskAsyncRequest,
  AdminUserDetailsResponse,
  AdminUserPageResponse,
  AdminRoleResponse,
  AdminUserResponse,
  AuthConfigResponse,
  AuthMeResponse,
  AuthRefreshResponse,
  AskJobResponse,
  AskJobSubmissionResponse,
  ChunkDetailsResponse,
  ChunkPageResponse,
  DocumentDetailsResponse,
  DocumentListPageResponse,
  CreateDocumentRequest,
  CreateDocumentResponse,
  DocumentStatsResponse,
  JobResponse
} from '../types/api';
import { getAccessToken } from '../../features/auth/session';

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
let refreshPromise: Promise<AuthRefreshResponse> | null = null;

async function readErrorMessage(response: Response): Promise<string> {
  const contentType = response.headers.get('content-type') ?? '';

  if (contentType.includes('application/json')) {
    const body = (await response.json()) as { message?: string; title?: string; detail?: string };
    return body.detail ?? body.message ?? body.title ?? `Request failed with status ${response.status}`;
  }

  const text = await response.text();
  return text || `Request failed with status ${response.status}`;
}

async function refreshAccessTokenInternal() {
  if (!refreshPromise) {
    refreshPromise = fetch(`${apiBaseUrl}/api/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json'
      }
    }).then(async (response) => {
      if (!response.ok) {
        throw new Error(await readErrorMessage(response));
      }

      return (await response.json()) as AuthRefreshResponse;
    }).finally(() => {
      refreshPromise = null;
    });
  }

  return refreshPromise;
}

function isAuthEndpoint(path: string) {
  return path.startsWith('/api/auth/');
}

async function request<T>(path: string, init?: RequestInit, allowRefresh = true): Promise<T> {
  const token = getAccessToken();
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    credentials: init?.credentials ?? 'same-origin',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {})
    }
  });

  if (response.status === 401 && allowRefresh && !isAuthEndpoint(path)) {
    const refreshed = await refreshAccessTokenInternal();
    const { setAccessToken } = await import('../../features/auth/session');
    setAccessToken(refreshed.accessToken);
    return request<T>(path, init, false);
  }

  if (!response.ok) {
    throw new Error(await readErrorMessage(response));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function getAuthConfig() {
  return request<AuthConfigResponse>('/api/auth/config');
}

export function refreshAccessToken() {
  return refreshAccessTokenInternal();
}

export function getCurrentUser() {
  return request<AuthMeResponse>('/api/auth/me');
}

export function getAdminUsers(params?: {
  search?: string;
  status?: string;
  sort?: string;
  page?: number;
  pageSize?: number;
}) {
  const query = new URLSearchParams();

  if (params?.search) {
    query.set('search', params.search);
  }

  if (params?.status) {
    query.set('status', params.status);
  }

  if (params?.sort) {
    query.set('sort', params.sort);
  }

  if (params?.page) {
    query.set('page', String(params.page));
  }

  if (params?.pageSize) {
    query.set('pageSize', String(params.pageSize));
  }

  const suffix = query.size > 0 ? `?${query.toString()}` : '';
  return request<AdminUserPageResponse>(`/api/admin/users${suffix}`);
}

export function getAdminRoles() {
  return request<AdminRoleResponse[]>('/api/admin/roles');
}

export function updateAdminUserRoles(userId: string, roles: string[]) {
  return request<AdminUserResponse>(`/api/admin/users/${userId}/roles`, {
    method: 'PUT',
    body: JSON.stringify({ roles })
  });
}

export function getAdminUser(userId: string) {
  return request<AdminUserDetailsResponse>(`/api/admin/users/${userId}`);
}

export function blockAdminUser(userId: string) {
  return request<AdminUserDetailsResponse>(`/api/admin/users/${userId}/block`, {
    method: 'POST'
  });
}

export function unblockAdminUser(userId: string) {
  return request<AdminUserDetailsResponse>(`/api/admin/users/${userId}/unblock`, {
    method: 'POST'
  });
}

export function logout() {
  return request<void>('/api/auth/logout', {
    method: 'POST',
    credentials: 'include'
  });
}

export function createDocument(payload: CreateDocumentRequest) {
  return request<CreateDocumentResponse>('/api/documents', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
}

export function getDocumentStats() {
  return request<DocumentStatsResponse>('/api/documents/stats');
}

export function getDocuments(page = 1, pageSize = 20) {
  return request<DocumentListPageResponse>(`/api/documents?page=${page}&pageSize=${pageSize}`);
}

export function getDocument(documentId: string) {
  return request<DocumentDetailsResponse>(`/api/documents/${documentId}`);
}

export function getDocumentChunks(documentId: string, page = 1, pageSize = 20) {
  return request<ChunkPageResponse>(`/api/documents/${documentId}/chunks?page=${page}&pageSize=${pageSize}`);
}

export function getChunk(chunkId: string) {
  return request<ChunkDetailsResponse>(`/api/chunks/${chunkId}`);
}

export function getJob(jobId: string) {
  return request<JobResponse>(`/api/jobs/${jobId}`);
}

export function submitAskJob(payload: AskAsyncRequest, actorKey: string, idempotencyKey: string) {
  return request<AskJobSubmissionResponse>('/api/ask/async', {
    method: 'POST',
    body: JSON.stringify(payload),
    headers: {
      'X-Actor-Key': actorKey,
      'Idempotency-Key': idempotencyKey
    }
  });
}

export function getAskJob(jobId: string) {
  return request<AskJobResponse>(`/api/ask/jobs/${jobId}`);
}

export function createAskEventStream(jobId: string) {
  return new EventSource(`${apiBaseUrl}/api/ask/jobs/${jobId}/events`);
}
