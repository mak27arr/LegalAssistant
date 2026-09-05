import type {
  AskAsyncRequest,
  AdminUserDetailsResponse,
  AdminUserPageResponse,
  AdminRoleResponse,
  AdminUserResponse,
  AuthConfigResponse,
  AuthCsrfResponse,
  AuthMeResponse,
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

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
let csrfToken: string | null = null;
let csrfPromise: Promise<string> | null = null;

async function readErrorMessage(response: Response): Promise<string> {
  const contentType = response.headers.get('content-type') ?? '';

  if (contentType.includes('application/json')) {
    const body = (await response.json()) as { message?: string; title?: string; detail?: string };
    return body.detail ?? body.message ?? body.title ?? `Request failed with status ${response.status}`;
  }

  const text = await response.text();
  return text || `Request failed with status ${response.status}`;
}

function isUnsafeMethod(method: string | undefined) {
  const normalized = (method ?? 'GET').toUpperCase();
  return normalized !== 'GET' && normalized !== 'HEAD' && normalized !== 'OPTIONS' && normalized !== 'TRACE';
}

async function getCsrfToken() {
  if (csrfToken) {
    return csrfToken;
  }

  if (!csrfPromise) {
    csrfPromise = fetch(`${apiBaseUrl}/api/auth/csrf`, {
      method: 'GET',
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json'
      }
    }).then(async (response) => {
      if (!response.ok) {
        throw new Error(await readErrorMessage(response));
      }

      const body = (await response.json()) as AuthCsrfResponse;
      csrfToken = body.token;
      return body.token;
    }).finally(() => {
      csrfPromise = null;
    });
  }

  return csrfPromise;
}

async function request<T>(path: string, init?: RequestInit, allowCsrfRetry = true): Promise<T> {
  const unsafe = isUnsafeMethod(init?.method);
  const headers = new Headers(init?.headers);
  if (!headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  if (unsafe) {
    headers.set('X-CSRF-TOKEN', await getCsrfToken());
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    credentials: init?.credentials ?? 'same-origin',
    headers
  });

  if (response.status === 400 && unsafe && allowCsrfRetry) {
    csrfToken = null;
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
    method: 'POST'
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

export function submitAskJob(payload: AskAsyncRequest, idempotencyKey: string) {
  return request<AskJobSubmissionResponse>('/api/ask/async', {
    method: 'POST',
    body: JSON.stringify(payload),
    headers: {
      'Idempotency-Key': idempotencyKey
    }
  });
}

export function getAskJob(jobId: string) {
  return request<AskJobResponse>(`/api/ask/jobs/${jobId}`);
}

export function createAskEventStream(jobId: string) {
  return new EventSource(`${apiBaseUrl}/api/ask/jobs/${jobId}/events`, {
    withCredentials: true
  });
}
