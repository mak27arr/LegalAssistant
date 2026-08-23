export function readStorage<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

export function writeStorage<T>(key: string, value: T): void {
  localStorage.setItem(key, JSON.stringify(value));
}

export function ensureStorageKey(key: string, factory: () => string): string {
  const existing = localStorage.getItem(key);
  if (existing) {
    return existing;
  }

  const created = factory();
  localStorage.setItem(key, created);
  return created;
}
