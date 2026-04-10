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
    return !!localStorage.getItem('token');
  }

  getRole(): string | null {
    return localStorage.getItem('userRole');
  }

  setRole(role: string): void {
    localStorage.setItem('userRole', role);
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

  private formatDisplayUserName(userName: string): string {
    return userName
      .trim()
      .split(/[\s._-]+/)
      .filter(Boolean)
      .map(part => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
      .join(' ');
  }
}
