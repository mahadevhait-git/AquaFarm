import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthService {
  setToken(token: string): void {
    localStorage.setItem('token', token);
  }

  getToken(): string | null {
    return localStorage.getItem('token');
  }

  removeToken(): void {
    localStorage.removeItem('token');
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) {
      return false;
    }

    if (this.isTokenExpired(token)) {
      this.clearSession();
      return false;
    }

    return true;
  }

  getRole(): string | null {
    return localStorage.getItem('userRole');
  }

  setRole(role: string): void {
    localStorage.setItem('userRole', role);
  }

  clearSession(): void {
    localStorage.removeItem('token');
    localStorage.removeItem('userRole');
  }

  getUserName(): string {
    const token = this.getToken();
    if (!token) {
      return 'Unknown User';
    }

    const payload = this.parseJwtPayload(token);
    const userName = payload?.['sub'];
    if (typeof userName !== 'string' || !userName.trim()) {
      return 'Unknown User';
    }

    return this.formatDisplayUserName(userName);
  }

  getUserId(): string | null {
    const token = this.getToken();
    if (!token) {
      return null;
    }

    const payload = this.parseJwtPayload(token);
    const candidateKeys = [
      'nameid',
      'nameidentifier',
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
    ];

    for (const key of candidateKeys) {
      const value = payload?.[key];
      if (typeof value === 'string' && value.trim()) {
        return value.trim();
      }
    }

    return null;
  }

  private parseJwtPayload(token: string): Record<string, unknown> | null {
    try {
      const parts = token.split('.');
      if (parts.length < 2) {
        return null;
      }

      const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
      const json = atob(padded);
      return JSON.parse(json) as Record<string, unknown>;
    } catch {
      return null;
    }
  }

  private isTokenExpired(token: string): boolean {
    const payload = this.parseJwtPayload(token);
    const exp = payload?.['exp'];

    if (typeof exp !== 'number') {
      return true;
    }

    const nowInSeconds = Math.floor(Date.now() / 1000);
    return exp <= nowInSeconds;
  }

  private formatDisplayUserName(userName: string): string {
    return userName
      .trim()
      .split(/[\s._-]+/)
      .filter(Boolean)
      .map(part => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
      .join(' ');
  }
}
