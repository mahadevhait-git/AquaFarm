import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../services/api.service';

type AuditRow = {
  id: string;
  recordType: string;
  actionType: string;
  recordId: string;
  groupId?: string | null;
  pondId?: string | null;
  farmerId?: string | null;
  oldAmount?: number | null;
  newAmount?: number | null;
  oldValuesJson?: string | null;
  newValuesJson?: string | null;
  performedById: string;
  performedByUserName: string;
  createdAt: string;
};

@Component({
  selector: 'app-admin-audit-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-audit-page.component.html',
})
export class AdminAuditPageComponent implements OnInit {
  rows: AuditRow[] = [];
  loading = true;
  errorMessage = '';

  constructor(private apiService: ApiService) {}

  ngOnInit(): void {
    this.loadRows();
  }

  private async loadRows(): Promise<void> {
    this.loading = true;
    this.errorMessage = '';
    try {
      const response = await firstValueFrom(this.apiService.adminAudit.investmentExpenseLogs());
      this.rows = Array.isArray(response) ? response : [];
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load audit logs.');
      this.rows = [];
    } finally {
      this.loading = false;
    }
  }

  private getErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error;
      }
      if (error.error?.message && typeof error.error.message === 'string') {
        return error.error.message;
      }
    }
    return fallback;
  }
}
