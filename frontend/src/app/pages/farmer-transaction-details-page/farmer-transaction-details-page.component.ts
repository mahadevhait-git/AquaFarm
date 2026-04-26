import { Component } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { Pond } from '../../models';
import { AuthService } from '../../services/auth.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

type FarmerTransactionDetailRow = {
  id: string; // capitalTransactionId
  contributionDate: string;
  principalAmount: number;
  interestToDate: number;
  totalAmount: number;
  canSelect?: boolean;
  status?: string;
  paidAt?: string | null;
  confirmedAt?: string | null;
  selected?: boolean;
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
  isPayoutEditor = false;

  rows: FarmerTransactionDetailRow[] = [];
  principalAmount = 0;
  totalInterest = 0;
  totalAmount = 0;
  selectedCount = 0;

  loading = true;
  errorMessage = '';
  successMessage = '';
  submittingPayouts = false;

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
    private route: ActivatedRoute,
    private location: Location,
  ) {}

  async ngOnInit(): Promise<void> {
    this.pondId = this.route.snapshot.paramMap.get('pondId') ?? '';
    this.farmerId = this.route.snapshot.paramMap.get('farmerId') ?? '';
    this.farmerName = this.route.snapshot.queryParamMap.get('name') ?? 'Farmer';
    this.userRole = (this.authService.getRole() ?? '').toLowerCase();
    this.isPayoutEditor = this.userRole === 'admin' || this.userRole === 'groupmanager';

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
      const pondListData = await firstValueFrom(this.apiService.ponds.list());
      const ponds = Array.isArray(pondListData) ? (pondListData as Pond[]) : [];
      const pond = ponds.find(p => p.id === this.pondId);
      const groupId = pond?.groupId ?? '';

      this.pondName = pond?.name ?? '';
      if (!groupId) {
        this.rows = [];
        this.recalculateTotals();
        this.errorMessage = 'No group linked to selected pond.';
        return;
      }

      if (this.isPayoutEditor) {
        await this.loadPayoutSetup(this.appliedInterestPercentage);
      } else {
        const transactionData = await firstValueFrom(this.apiService.groups.capitalTransactions(groupId, this.farmerId));
        const transactions = Array.isArray(transactionData) ? transactionData : [];
        this.rows = transactions.map((item: any) => ({
          id: String(item.id),
          contributionDate: String(item.contributionDate ?? ''),
          principalAmount: Number(item.amount ?? 0),
          interestToDate: 0,
          totalAmount: Number(item.amount ?? 0),
          selected: false,
        }));
      }

      this.recalculateTotals();
      this.errorMessage = '';
      this.successMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load farmer transaction details.');
      this.rows = [];
      this.recalculateTotals();
    } finally {
      this.loading = false;
    }
  }

  async applyInterestCalculation(): Promise<void> {
    const rate = Number(this.interestPercentage ?? 0);
    this.appliedInterestPercentage = Number.isFinite(rate) && rate > 0 ? rate : 0;
    if (this.isPayoutEditor) {
      try {
        await this.loadPayoutSetup(this.appliedInterestPercentage);
        this.recalculateTotals();
        this.errorMessage = '';
      } catch (error) {
        this.errorMessage = this.getErrorMessage(error, 'Failed to recalculate payout setup.');
      }
      return;
    }

    const annualRate = this.appliedInterestPercentage / 100;
    this.rows = this.rows.map(row => {
      const interest = this.calculateSimpleInterestToDate(row.principalAmount, row.contributionDate, annualRate);
      return {
        ...row,
        interestToDate: interest,
        totalAmount: row.principalAmount + interest,
      };
    });
    this.recalculateTotals();
  }

  toggleRowSelection(row: FarmerTransactionDetailRow): void {
    if (!this.isPayoutEditor || !row.canSelect) {
      return;
    }
    row.selected = !row.selected;
    this.selectedCount = this.rows.filter(r => !!r.selected).length;
  }

  async createPayouts(): Promise<void> {
    if (!this.isPayoutEditor) {
      return;
    }

    const selectedIds = this.rows
      .filter(r => !!r.selected && r.canSelect)
      .map(r => r.id);

    if (selectedIds.length === 0) {
      this.errorMessage = 'Please select at least one row.';
      this.successMessage = '';
      return;
    }

    this.submittingPayouts = true;
    this.errorMessage = '';
    this.successMessage = '';
    try {
      await firstValueFrom(this.apiService.groups.createPayouts({
        pondId: this.pondId,
        farmerId: this.farmerId,
        annualInterestRate: this.appliedInterestPercentage,
        capitalTransactionIds: selectedIds,
      }));
      this.successMessage = 'Payout entries submitted successfully.';
      await this.loadPayoutSetup(this.appliedInterestPercentage);
      this.recalculateTotals();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to submit payout entries.');
    } finally {
      this.submittingPayouts = false;
    }
  }

  getStatusClass(status: string | undefined): string {
    const normalized = (status || '').toLowerCase();
    if (normalized === 'completed') {
      return 'status-completed';
    }
    if (normalized === 'rejected') {
      return 'status-rejected';
    }
    return 'status-pending';
  }

  private recalculateTotals(): void {
    this.principalAmount = this.rows.reduce((sum, row) => sum + row.principalAmount, 0);
    this.totalInterest = this.rows.reduce((sum, row) => sum + row.interestToDate, 0);
    this.totalAmount = this.rows.reduce((sum, row) => sum + row.totalAmount, 0);
    this.selectedCount = this.rows.filter(r => !!r.selected).length;
  }

  private async loadPayoutSetup(annualInterestRatePercentage: number): Promise<void> {
    const response = await firstValueFrom(this.apiService.groups.payoutSetup(
      this.pondId,
      this.farmerId,
      annualInterestRatePercentage,
    ));

    const setupRows = Array.isArray(response?.rows) ? response.rows : [];
    this.rows = setupRows.map((item: any) => ({
      id: String(item.capitalTransactionId ?? ''),
      contributionDate: String(item.contributionDate ?? ''),
      principalAmount: Number(item.principalAmount ?? 0),
      interestToDate: Number(item.interestAmount ?? 0),
      totalAmount: Number(item.totalAmount ?? 0),
      canSelect: !!item.canSelect,
      status: String(item.status ?? ''),
      paidAt: item.paidAt ? String(item.paidAt) : null,
      confirmedAt: item.confirmedAt ? String(item.confirmedAt) : null,
      selected: false,
    }));
  }

  private calculateSimpleInterestToDate(principal: number, contributionDateValue: string, annualRate: number): number {
    if (!contributionDateValue || !Number.isFinite(principal) || principal <= 0) {
      return 0;
    }

    const contributionRawDate = new Date(contributionDateValue);
    const contributionDate = new Date(
      contributionRawDate.getFullYear(),
      contributionRawDate.getMonth(),
      contributionRawDate.getDate(),
    );
    if (Number.isNaN(contributionDate.getTime())) {
      return 0;
    }

    const nowRawDate = new Date();
    const now = new Date(nowRawDate.getFullYear(), nowRawDate.getMonth(), nowRawDate.getDate());
    const millis = now.getTime() - contributionDate.getTime();
    if (millis <= 0) {
      return 0;
    }

    const days = millis / (1000 * 60 * 60 * 24);
    const monthlyRate = annualRate / 12;
    const monthsElapsed = days / 30;
    return principal * monthlyRate * monthsElapsed;
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
