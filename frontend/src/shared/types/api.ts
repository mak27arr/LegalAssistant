export interface CreateDocumentRequest {
  title: string;
  url: string;
  content: string;
  metadata: unknown;
}

export interface CreateDocumentResponse {
  jobId: string;
  documentId: string;
}

export interface DocumentStatsResponse {
  totalDocuments: number;
  queuedJobs: number;
  inProgressJobs: number;
  completedJobs: number;
  failedJobs: number;
}

export interface JobResponse {
  id: string;
  type: string;
  status: string;
  payload: string;
  result: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AskAsyncRequest {
  question: string;
  topK?: number;
  conversationId?: string | null;
}

export interface AskResponse {
  question: string;
  answer: string;
  isGrounded: boolean;
}

export interface AskJobSubmissionResponse {
  jobId: string;
  status: string;
  isNew: boolean;
  actorScopeKey: string;
  idempotencyKey: string;
  createdAt: string;
  updatedAt: string;
}

export interface AskJobResponse {
  jobId: string;
  status: string;
  actorScopeKey: string;
  idempotencyKey: string;
  question: string;
  topK: number;
  conversationId: string | null;
  error: string | null;
  result: AskResponse | null;
  createdAt: string;
  updatedAt: string;
}
