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

    members: (groupId: string, search = ''): Observable<any> =>
      this.http.get(`${this.apiBaseUrl}/groups/${groupId}/members`, {
        headers: this.authHeaders(),
        params: search ? { search } : {},
      }),

    memberCandidates: (groupId: string, search = ''): Observable<any> =>
      this.http.get(`${this.apiBaseUrl}/groups/${groupId}/member-candidates`, {
        headers: this.authHeaders(),
        params: search ? { search } : {},
      }),

    addMember: (groupId: string, userId: string): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/groups/${groupId}/members`, { userId }, { headers: this.authHeaders() }),

    removeMember: (groupId: string, userId: string): Observable<any> =>
      this.http.delete(`${this.apiBaseUrl}/groups/${groupId}/members/${userId}`, { headers: this.authHeaders() }),

    contributions: (groupId: string): Observable<any> =>
      this.http.get(`${this.apiBaseUrl}/groups/${groupId}/contributions`, { headers: this.authHeaders() }),

    recordContribution: (groupId: string, userId: string, amount: number): Observable<any> =>
      this.http.post(`${this.apiBaseUrl}/groups/${groupId}/contributions/record`, { userId, amount }, {
        headers: this.authHeaders(),
      }),

    upsertContribution: (groupId: string, userId: string, amount: number): Observable<any> =>
      this.http.put(`${this.apiBaseUrl}/groups/${groupId}/contributions`, { userId, amount }, {
        headers: this.authHeaders(),
      }),

    deleteContribution: (groupId: string, userId: string): Observable<any> =>
      this.http.delete(`${this.apiBaseUrl}/groups/${groupId}/contributions/${userId}`, { headers: this.authHeaders() }),

    capitalTransactions: (groupId: string, farmerId?: string): Observable<any> =>
      this.http.get(`${this.apiBaseUrl}/groups/${groupId}/capital-transactions`, {
        headers: this.authHeaders(),
        params: farmerId ? { farmerId } : {},
      }),

    updateCapitalTransactionAmount: (groupId: string, transactionId: string, amount: number): Observable<any> =>
      this.http.put(`${this.apiBaseUrl}/groups/${groupId}/capital-transactions/${transactionId}`, { amount }, {
        headers: this.authHeaders(),
      }),
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

  expenses = {
    list: (pondId?: string): Observable<any> =>
      this.http.get(`${this.apiBaseUrl}/expenses`, {
        headers: this.authHeaders(),
        params: pondId ? { pondId } : {},
      }),

    create: (payload: { pondId: string; amount: number; purpose: string; date: string; bills?: File[] | null }): Observable<any> => {
      const formData = new FormData();
      formData.append('pondId', payload.pondId);
      formData.append('amount', payload.amount.toString());
      formData.append('purpose', payload.purpose);
      formData.append('date', payload.date);
      if (payload.bills && payload.bills.length > 0) {
        for (const bill of payload.bills) {
          formData.append('bills', bill);
        }
      }

      return this.http.post(`${this.apiBaseUrl}/expenses`, formData, { headers: this.authHeaders() });
    },

    downloadBill: (expenseId: string): Observable<Blob> =>
      this.http.get(`${this.apiBaseUrl}/expenses/${expenseId}/bill`, {
        headers: this.authHeaders(),
        responseType: 'blob',
      }),

    expenseBills: (expenseId: string): Observable<any> =>
      this.http.get(`${this.apiBaseUrl}/expenses/${expenseId}/bills`, { headers: this.authHeaders() }),

    uploadExpenseBill: (expenseId: string, bill: File): Observable<any> => {
      const formData = new FormData();
      formData.append('bill', bill);
      return this.http.post(`${this.apiBaseUrl}/expenses/${expenseId}/bills`, formData, {
        headers: this.authHeaders(),
      });
    },

    downloadExpenseBill: (billId: string): Observable<Blob> =>
      this.http.get(`${this.apiBaseUrl}/expenses/expense-bills/${billId}/download`, {
        headers: this.authHeaders(),
        responseType: 'blob',
      }),

    pondBills: (pondId: string): Observable<any> =>
      this.http.get(`${this.apiBaseUrl}/expenses/ponds/${pondId}/bills`, { headers: this.authHeaders() }),

    uploadPondBill: (pondId: string, bill: File): Observable<any> => {
      const formData = new FormData();
      formData.append('bill', bill);
      return this.http.post(`${this.apiBaseUrl}/expenses/ponds/${pondId}/bills`, formData, {
        headers: this.authHeaders(),
      });
    },

    downloadPondBill: (billId: string): Observable<Blob> =>
      this.http.get(`${this.apiBaseUrl}/expenses/pond-bills/${billId}/download`, {
        headers: this.authHeaders(),
        responseType: 'blob',
      }),
  };
}
