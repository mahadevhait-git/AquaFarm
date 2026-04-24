import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Expense, Group, Pond } from '../../models';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

type FarmerContributionRow = {
  userId: string;
  name: string;
  totalContribution: number;
};

type ExpenseCategoryRow = {
  category: string;
  totalSpent: number;
};

@Component({
  selector: 'app-investment-summary-page',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe],
  templateUrl: './investment-summary-page.component.html',
})
export class InvestmentSummaryPageComponent {
  groups: Group[] = [];
  ponds: Pond[] = [];
  selectedPondId = '';
  farmerRows: FarmerContributionRow[] = [];
  expenseCategoryRows: ExpenseCategoryRow[] = [];
  loadingGroups = true;
  loadingSummary = false;
  errorMessage = '';
  userRole = '';
  userId = '';

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
    private location: Location,
    private router: Router,
  ) {}

  async ngOnInit(): Promise<void> {
    this.userRole = this.authService.getRole() ?? '';
    this.userId = this.authService.getUserId() ?? '';
    await this.loadGroups();
  }

  async loadGroups(): Promise<void> {
    this.loadingGroups = true;
    try {
      const [groupData, pondData] = await Promise.all([
        firstValueFrom(this.apiService.groups.list()),
        firstValueFrom(this.apiService.ponds.list()),
      ]);
      this.groups = Array.isArray(groupData) ? groupData : [];
      this.ponds = Array.isArray(pondData) ? pondData : [];
      this.selectedPondId = '';
      this.farmerRows = [];
      this.expenseCategoryRows = [];
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load ponds.');
    } finally {
      this.loadingGroups = false;
    }
  }

  async onPondChange(): Promise<void> {
    await this.loadSummary();
  }

  async loadSummary(): Promise<void> {
    const groupId = this.getGroupIdForPond(this.selectedPondId);
    if (!groupId) {
      this.farmerRows = [];
      this.expenseCategoryRows = [];
      this.errorMessage = '';
      return;
    }

    this.loadingSummary = true;
    try {
      const [contributionData, expenseData] = await Promise.all([
        firstValueFrom(this.apiService.groups.contributions(groupId)),
        firstValueFrom(this.apiService.expenses.list(this.selectedPondId)),
      ]);

      const rows = (Array.isArray(contributionData) ? contributionData : []).map((item: any) => ({
        userId: item.userId,
        name: item.name,
        totalContribution: Number(item.investedAmount ?? 0),
      }));
      this.farmerRows = rows;
      this.expenseCategoryRows = this.getExpenseCategoryRows(Array.isArray(expenseData) ? expenseData : []);
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load summary data.');
    } finally {
      this.loadingSummary = false;
    }
  }

  get totalContributionAmount(): number {
    return this.farmerRows.reduce((sum, row) => sum + row.totalContribution, 0);
  }

  get totalExpensesAmount(): number {
    return this.expenseCategoryRows.reduce((sum, row) => sum + row.totalSpent, 0);
  }

  get totalInvestmentAmount(): number {
    return this.totalContributionAmount;
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

  goBack(): void {
    this.location.back();
  }

  async openFarmerDetails(row: FarmerContributionRow): Promise<void> {
    if (!this.selectedPondId) {
      this.errorMessage = 'Please select a pond first.';
      return;
    }

    await this.router.navigate(['/loans/summary', this.selectedPondId, 'farmer', row.userId], {
      queryParams: { name: row.name },
    });
  }

  private getGroupIdForPond(pondId: string): string {
    const pond = this.ponds.find(p => p.id === pondId);
    return pond?.groupId ?? '';
  }

  private getExpenseCategoryRows(expenses: Expense[]): ExpenseCategoryRow[] {
    const categoryMap = new Map<string, number>();

    for (const expense of expenses) {
      const rawCategory = typeof expense.purpose === 'string' ? expense.purpose.trim() : '';
      const category = rawCategory || 'Uncategorized';
      categoryMap.set(category, (categoryMap.get(category) ?? 0) + Number(expense.amount ?? 0));
    }

    return Array.from(categoryMap.entries())
      .map(([category, totalSpent]) => ({ category, totalSpent }))
      .sort((a, b) => b.totalSpent - a.totalSpent);
  }
}
