import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Group, Pond } from '../../models';
import { ApiService } from '../../services/api.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

type FarmerInvestmentRow = {
  userId: string;
  name: string;
  investedAmount: number;
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
  rows: FarmerInvestmentRow[] = [];
  loadingGroups = true;
  loadingRows = false;
  errorMessage = '';

  constructor(
    private apiService: ApiService,
    private location: Location,
  ) {}

  async ngOnInit(): Promise<void> {
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
      this.selectedPondId = this.ponds[0]?.id ?? '';
      await this.loadRows();
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load ponds.');
    } finally {
      this.loadingGroups = false;
    }
  }

  async onGroupChange(): Promise<void> {
    await this.loadRows();
  }

  async loadRows(): Promise<void> {
    const groupId = this.getGroupIdForPond(this.selectedPondId);
    if (!groupId) {
      this.rows = [];
      return;
    }

    this.loadingRows = true;
    try {
      const data = await firstValueFrom(this.apiService.groups.contributions(groupId));
      this.rows = (Array.isArray(data) ? data : []).map((item: any) => ({
        userId: item.userId,
        name: item.name,
        investedAmount: Number(item.investedAmount ?? 0),
      }));
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load farmer investments.');
    } finally {
      this.loadingRows = false;
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

  goBack(): void {
    this.location.back();
  }

  private getGroupIdForPond(pondId: string): string {
    const pond = this.ponds.find(p => p.id === pondId);
    return pond?.groupId ?? '';
  }
}
