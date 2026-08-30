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

export interface DocumentListItemResponse {
  id: string;
  title: string;
  url: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  chunkCount: number;
  processingStatus: string | null;
}

export interface DocumentListPageResponse {
  items: DocumentListItemResponse[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface DocumentDetailsResponse {
  id: string;
  title: string;
  url: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  chunkCount: number;
}

export interface ChunkListItemResponse {
  chunkId: string;
  documentId: string;
  chunkIndex: number;
  charRange: string;
  sourceUrl: string;
  createdAt: string;
  hasEmbedding: boolean;
  preview: string;
}

export interface ChunkPageResponse {
  items: ChunkListItemResponse[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface ChunkDetailsResponse {
  chunkId: string;
  documentId: string;
  chunkIndex: number;
  text: string;
  charRange: string;
  sourceUrl: string;
  createdAt: string;
  hasEmbedding: boolean;
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

export interface AuthConfigResponse {
  providers: {
    google: {
      enabled: boolean;
      loginUrl: string | null;
    };
  };
}

export interface AuthMeResponse {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface AuthRefreshResponse {
  accessToken: string;
  expiresAtUtc: string;
}
