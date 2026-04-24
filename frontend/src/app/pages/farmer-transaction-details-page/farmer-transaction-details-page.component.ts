import { Component } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

type FarmerTransactionDetailRow = {
  id: string;
  payoutId?: string;
  contributionDate: string;
  paymentDate?: string;
  principalAmount: number;
  annualInterestRate?: number;
  interestToDate: number;
  totalAmount: number;
  canSelect: boolean;
  status: string;
  managerName?: string;
  pondName?: string;
  selected: boolean;
};

@Component({
  selector: 'app-farmer-transaction-details-page',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe],
  templateUrl: './farmer-transaction-details-page.component.html',
})
export class FarmerTransactionDetailsPageComponent {
  pondId = '';
  farmerId = '';
  farmerName = 'Farmer';
  pondName = '';
  interestPercentage = 0;
  appliedInterestPercentage = 0;
  userRole = '';
  userId = '';
  submitting = false;
  confirmingPayoutId: string | null = null;

  rows: FarmerTransactionDetailRow[] = [];
  principalAmount = 0;
  totalInterest = 0;
  totalAmount = 0;

  loading = true;
  errorMessage = '';

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
    private route: ActivatedRoute,
    private location: Location,
  ) {}

  async ngOnInit(): Promise<void> {
    this.pondId = this.route.snapshot.paramMap.get('pondId') ?? '';
    const routeFarmerId = this.route.snapshot.paramMap.get('farmerId') ?? '';
    this.farmerId = routeFarmerId;
    this.farmerName = this.route.snapshot.queryParamMap.get('name') ?? 'Farmer';
    this.userRole = this.authService.getRole() ?? '';
    this.userId = this.authService.getUserId() ?? '';
    if (this.userRole === 'Farmer') {
      // Prefer token user id, but never wipe a valid route id with empty value.
      this.farmerId = this.userId || routeFarmerId;
    }

    await this.loadDetails();
  }

  goBack(): void {
    this.location.back();
  }

  private async loadDetails(): Promise<void> {
    if (!this.pondId || !this.farmerId) {
      this.errorMessage = 'Missing farmer details.';
      this.loading = false;
      return;
    }

    this.loading = true;
    try {
      if (this.userRole === 'Farmer') {
        const response = await firstValueFrom(this.apiService.groups.farmerPayouts(this.pondId));
        const payouts = (Array.isArray(response) ? response : []).filter((item: any) => item.farmerId === this.farmerId);
        this.rows = payouts.map((item: any) => ({
          id: String(item.capitalTransactionId),
          payoutId: String(item.payoutId),
          contributionDate: String(item.contributionDate ?? ''),
          principalAmount: Number(item.principalAmount ?? 0),
          annualInterestRate: Number(item.annualInterestRate ?? 0),
          interestToDate: Number(item.interestAmount ?? 0),
          totalAmount: Number(item.totalAmount ?? 0),
          canSelect: false,
          status: String(item.status ?? 'Pending'),
          managerName: String(item.managerName ?? ''),
          pondName: String(item.pondName ?? ''),
          paymentDate: item.paidAt ? String(item.paidAt) : undefined,
          selected: false,
        }));
        this.pondName = this.rows[0]?.pondName ?? '';
      } else {
        const response = await firstValueFrom(this.apiService.groups.payoutSetup(this.pondId, this.farmerId, this.interestPercentage));
        this.farmerName = String(response?.farmerName ?? this.farmerName);
        this.pondName = String(response?.pondName ?? '');
        this.rows = (Array.isArray(response?.rows) ? response.rows : []).map((item: any) => ({
          id: String(item.capitalTransactionId),
          contributionDate: String(item.contributionDate ?? ''),
          principalAmount: Number(item.principalAmount ?? 0),
          annualInterestRate: Number(item.annualInterestRate ?? 0),
          interestToDate: Number(item.interestAmount ?? 0),
          totalAmount: Number(item.totalAmount ?? 0),
          canSelect: Boolean(item.canSelect),
          status: String(item.status ?? 'Unpaid'),
          paymentDate: item.paidAt ? String(item.paidAt) : undefined,
          selected: false,
        }));
      }

      this.recalculateTotals();
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load farmer transaction details.');
      this.rows = [];
      this.recalculateTotals();
    } finally {
      this.loading = false;
    }
  }

  applyInterestCalculation(): void {
    if (this.userRole === 'Farmer') {
      return;
    }

    const rate = Number(this.interestPercentage ?? 0);
    this.appliedInterestPercentage = Number.isFinite(rate) && rate > 0 ? rate : 0;
    this.reloadSetupForRate();
  }

  async onSelectionChange(): Promise<void> {
    this.recalculateTotals();
  }

  async submitPayout(): Promise<void> {
    if (this.userRole === 'Farmer') {
      return;
    }

    const selectedTransactionIds = this.rows
      .filter(row => row.canSelect && row.selected)
      .map(row => row.id);

    if (selectedTransactionIds.length === 0) {
      this.errorMessage = 'Please select at least one contribution entry.';
      return;
    }

    this.submitting = true;
    try {
      await firstValueFrom(this.apiService.groups.createPayouts({
        pondId: this.pondId,
        farmerId: this.farmerId,
        annualInterestRate: this.appliedInterestPercentage,
        capitalTransactionIds: selectedTransactionIds,
      }));
      this.errorMessage = '';
      await this.loadDetails();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to submit payout details.');
    } finally {
      this.submitting = false;
    }
  }

  async confirmPayout(row: FarmerTransactionDetailRow): Promise<void> {
    if (this.userRole !== 'Farmer' || !row.payoutId || row.status === 'Completed') {
      return;
    }

    this.confirmingPayoutId = row.payoutId;
    try {
      await firstValueFrom(this.apiService.groups.confirmPayout(row.payoutId));
      await this.loadDetails();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to confirm payment.');
    } finally {
      this.confirmingPayoutId = null;
    }
  }

  private async reloadSetupForRate(): Promise<void> {
    this.loading = true;
    try {
      const response = await firstValueFrom(this.apiService.groups.payoutSetup(this.pondId, this.farmerId, this.appliedInterestPercentage));
      this.rows = (Array.isArray(response?.rows) ? response.rows : []).map((item: any) => ({
        id: String(item.capitalTransactionId),
        contributionDate: String(item.contributionDate ?? ''),
        principalAmount: Number(item.principalAmount ?? 0),
        annualInterestRate: Number(item.annualInterestRate ?? 0),
        interestToDate: Number(item.interestAmount ?? 0),
        totalAmount: Number(item.totalAmount ?? 0),
        canSelect: Boolean(item.canSelect),
        status: String(item.status ?? 'Unpaid'),
        paymentDate: item.paidAt ? String(item.paidAt) : undefined,
        selected: false,
      }));
      this.recalculateTotals();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to apply interest.');
    } finally {
      this.loading = false;
    }
  }

  private recalculateTotals(): void {
    const sourceRows = this.userRole === 'Farmer'
      ? this.rows
      : this.rows.filter(row => row.canSelect && row.selected);
    this.principalAmount = sourceRows.reduce((sum, row) => sum + row.principalAmount, 0);
    this.totalInterest = sourceRows.reduce((sum, row) => sum + row.interestToDate, 0);
    this.totalAmount = sourceRows.reduce((sum, row) => sum + row.totalAmount, 0);
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
