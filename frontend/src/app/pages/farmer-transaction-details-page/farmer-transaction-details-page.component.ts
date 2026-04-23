import { Component } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Pond } from '../../models';
import { ApiService } from '../../services/api.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

type FarmerTransactionDetailRow = {
  id: string;
  contributionDate: string;
  amount: number;
  interestToDate: number;
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
  interestPercentage = 0;
  appliedInterestPercentage = 0;

  rows: FarmerTransactionDetailRow[] = [];
  principalAmount = 0;
  totalInterest = 0;
  totalAmount = 0;

  loading = true;
  errorMessage = '';

  constructor(
    private apiService: ApiService,
    private route: ActivatedRoute,
    private location: Location,
  ) {}

  async ngOnInit(): Promise<void> {
    this.pondId = this.route.snapshot.paramMap.get('pondId') ?? '';
    this.farmerId = this.route.snapshot.paramMap.get('farmerId') ?? '';
    this.farmerName = this.route.snapshot.queryParamMap.get('name') ?? 'Farmer';

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
      const ponds = await firstValueFrom(this.apiService.ponds.list());
      const pondList = Array.isArray(ponds) ? (ponds as Pond[]) : [];
      const selectedPond = pondList.find(p => p.id === this.pondId);
      const groupId = selectedPond?.groupId ?? '';
      if (!groupId) {
        this.rows = [];
        this.principalAmount = 0;
        this.totalInterest = 0;
        this.totalAmount = 0;
        this.errorMessage = 'No group linked to selected pond.';
        return;
      }

      const response = await firstValueFrom(this.apiService.groups.capitalTransactions(groupId, this.farmerId));
      const transactions = Array.isArray(response) ? response : [];

      this.rows = transactions.map((item: any) => {
        const amount = Number(item.amount ?? 0);
        const contributionDate = item.contributionDate ? String(item.contributionDate) : '';
        return {
          id: String(item.id),
          contributionDate,
          amount,
          interestToDate: 0,
        };
      });

      this.principalAmount = this.rows.reduce((sum, row) => sum + row.amount, 0);
      this.totalInterest = 0;
      this.totalAmount = this.principalAmount;
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load farmer transaction details.');
      this.rows = [];
      this.principalAmount = 0;
      this.totalInterest = 0;
      this.totalAmount = 0;
    } finally {
      this.loading = false;
    }
  }

  applyInterestCalculation(): void {
    const rate = Number(this.interestPercentage ?? 0);
    const annualRate = Number.isFinite(rate) && rate > 0 ? rate / 100 : 0;
    this.appliedInterestPercentage = Number.isFinite(rate) && rate > 0 ? rate : 0;

    this.rows = this.rows.map(row => ({
      ...row,
      interestToDate: this.calculateSimpleInterestToDate(row.amount, row.contributionDate, annualRate),
    }));

    this.totalInterest = this.rows.reduce((sum, row) => sum + row.interestToDate, 0);
    this.totalAmount = this.principalAmount + this.totalInterest;
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
