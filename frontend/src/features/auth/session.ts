let accessToken: string | null = null;

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(nextAccessToken: string | null): void {
  accessToken = nextAccessToken;
}
