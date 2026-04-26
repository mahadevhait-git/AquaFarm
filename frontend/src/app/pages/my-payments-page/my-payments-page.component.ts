import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Pond } from '../../models';
import { ApiService } from '../../services/api.service';

type FarmerPaymentRow = {
  payoutId: string;
  pondId: string;
  pondName: string;
  managerName: string;
  contributionDate: string;
  principalAmount: number;
  interestAmount: number;
  totalAmount: number;
  status: string;
  paidAt?: string;
  confirmedAt?: string;
};

@Component({
  selector: 'app-my-payments-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './my-payments-page.component.html',
})
export class MyPaymentsPageComponent {
  ponds: Pond[] = [];
  selectedPondId = '';
  rows: FarmerPaymentRow[] = [];
  loading = true;
  errorMessage = '';
  successMessage = '';
  confirmingPayoutId: string | null = null;
  rejectingPayoutId: string | null = null;

  constructor(private apiService: ApiService) {}

  async ngOnInit(): Promise<void> {
    await this.loadPondsAndPayments();
  }

  async onPondChange(): Promise<void> {
    await this.loadPayments();
  }

  async confirm(row: FarmerPaymentRow): Promise<void> {
    if (row.status === 'Completed' || row.status === 'Rejected') {
      return;
    }

    this.confirmingPayoutId = row.payoutId;
    try {
      await firstValueFrom(this.apiService.groups.confirmPayout(row.payoutId));
      this.successMessage = 'Payment confirmed successfully.';
      await this.loadPayments();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to confirm payment.');
      this.successMessage = '';
    } finally {
      this.confirmingPayoutId = null;
    }
  }

  async reject(row: FarmerPaymentRow): Promise<void> {
    if (row.status === 'Completed' || row.status === 'Rejected') {
      return;
    }

    const shouldReject = window.confirm('Are you sure you want to reject this payment?');
    if (!shouldReject) {
      return;
    }

    this.rejectingPayoutId = row.payoutId;
    try {
      await firstValueFrom(this.apiService.groups.rejectPayout(row.payoutId));
      this.successMessage = 'Payment rejected successfully.';
      await this.loadPayments();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to reject payment.');
      this.successMessage = '';
    } finally {
      this.rejectingPayoutId = null;
    }
  }

  getStatusClass(status: string): string {
    const normalized = (status || '').toLowerCase();
    if (normalized === 'completed') {
      return 'status-completed';
    }
    if (normalized === 'rejected') {
      return 'status-rejected';
    }
    return 'status-pending';
  }

  private async loadPondsAndPayments(): Promise<void> {
    this.loading = true;
    try {
      const pondsResponse = await firstValueFrom(this.apiService.ponds.list());
      this.ponds = Array.isArray(pondsResponse) ? pondsResponse : [];
      await this.loadPayments();
      this.errorMessage = '';
      this.successMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load my payments.');
      this.rows = [];
      this.successMessage = '';
    } finally {
      this.loading = false;
    }
  }

  private async loadPayments(): Promise<void> {
    try {
      const response = await firstValueFrom(this.apiService.groups.farmerPayouts(this.selectedPondId || undefined));
      this.rows = (Array.isArray(response) ? response : []).map((item: any) => ({
        payoutId: String(item.payoutId),
        pondId: String(item.pondId),
        pondName: String(item.pondName ?? ''),
        managerName: String(item.managerName ?? ''),
        contributionDate: String(item.contributionDate ?? ''),
        principalAmount: Number(item.principalAmount ?? 0),
        interestAmount: Number(item.interestAmount ?? 0),
        totalAmount: Number(item.totalAmount ?? 0),
        status: String(item.status ?? 'Pending'),
        paidAt: item.paidAt ? String(item.paidAt) : undefined,
        confirmedAt: item.confirmedAt ? String(item.confirmedAt) : undefined,
      }));
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load my payments.');
      this.rows = [];
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

      if (error.status === 0) {
        return 'Server is unreachable. Please check if the backend is running.';
      }
    }

    return fallback;
  }
}
