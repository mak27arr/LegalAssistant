import type {
  AskAsyncRequest,
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

async function readErrorMessage(response: Response): Promise<string> {
  const contentType = response.headers.get('content-type') ?? '';

  if (contentType.includes('application/json')) {
    const body = (await response.json()) as { message?: string; title?: string; detail?: string };
    return body.detail ?? body.message ?? body.title ?? `Request failed with status ${response.status}`;
  }

  const text = await response.text();
  return text || `Request failed with status ${response.status}`;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {})
    }
  });

  if (!response.ok) {
    throw new Error(await readErrorMessage(response));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
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
