export const STORAGE_KEYS = {
  TOKEN: 'token',
  REFRESH_TOKEN: 'refreshToken',
  ROLE: 'role',
  EMAIL: 'email',
} as const;

export function getToken(): string | null {
  return localStorage.getItem(STORAGE_KEYS.TOKEN);
}

export function getRole(): string | null {
  return localStorage.getItem(STORAGE_KEYS.ROLE);
}

export function clearAuth(): void {
  Object.values(STORAGE_KEYS).forEach(k => localStorage.removeItem(k));
}

export function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return payload.exp * 1000 < Date.now();
  } catch {
    return true;
  }
}

export function isAuthenticated(): boolean {
  const token = getToken();
  if (!token) return false;
  return !isTokenExpired(token);
}

export function decodeToken<T = Record<string, unknown>>(token: string): T | null {
  try {
    return JSON.parse(atob(token.split('.')[1])) as T;
  } catch {
    return null;
  }
}
