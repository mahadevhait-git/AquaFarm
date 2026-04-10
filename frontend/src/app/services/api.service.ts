import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly apiBaseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  private authHeaders(): HttpHeaders {
    return new HttpHeaders({
      Authorization: `Bearer ${localStorage.getItem('token') ?? ''}`,
    });
  }

  auth = {
    register: (
      firstName: string,
      lastName: string,
      address: string,
      email: string,
      phoneNumber: string,
      password: string,
      role: string,
    ): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/auth/register`, {
        firstName,
        lastName,
        address,
        email: email || null,
        phoneNumber,
        password,
        role,
      }),

    login: (phoneNumber: string, password: string): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/auth/login`, { phoneNumber, password }),

    requestForgotPasswordOtp: (email: string): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/auth/forgot-password/request-otp`, { email }),

    resetForgotPassword: (email: string, otp: string, newPassword: string): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/auth/forgot-password/reset`, { email, otp, newPassword }),
  };

  ponds = {
    list: (): Observable<any> => this.http.get(`${this.apiBaseUrl}/ponds`, { headers: this.authHeaders() }),

    get: (id: string): Observable<any> =>
      this.http.get(`${this.apiBaseUrl}/ponds/${id}`, { headers: this.authHeaders() }),

    create: (pond: any): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/ponds`, pond, { headers: this.authHeaders() }),

    update: (id: string, pond: any): Observable<any> =>
      this.http.put(`${this.apiBaseUrl}/ponds/${id}`, pond, { headers: this.authHeaders() }),

    delete: (id: string): Observable<any> =>
      this.http.delete(`${this.apiBaseUrl}/ponds/${id}`, { headers: this.authHeaders() }),
  };

  groups = {
    list: (): Observable<any> => this.http.get(`${this.apiBaseUrl}/groups`, { headers: this.authHeaders() }),

    create: (group: any): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/groups`, group, { headers: this.authHeaders() }),
  };

  loans = {
    create: (loanRequest: any): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/loans`, loanRequest, { headers: this.authHeaders() }),

    repay: (loanId: string, repaymentRequest: any): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/loans/${loanId}/repay`, repaymentRequest, {
        headers: this.authHeaders(),
      }),

    summary: (loanId: string): Observable<any> =>
      this.http.get(`${this.apiBaseUrl}/loans/${loanId}/summary`, { headers: this.authHeaders() }),
  };
}
