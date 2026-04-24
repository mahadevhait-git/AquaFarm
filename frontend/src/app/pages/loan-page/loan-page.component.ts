import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Group, GroupMember, Pond } from '../../models';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

type CapitalTransactionRow = {
  id: string;
  farmerId: string;
  farmerName: string;
  contributionDate: string;
  amount: number;
};

@Component({
  selector: 'app-loan-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, I18nPipe],
  templateUrl: './loan-page.component.html',
})
export class LoanPageComponent {
  groups: Group[] = [];
  ponds: Pond[] = [];
  showEntryForm = false;
  loadingGroups = true;
  loadingFarmers = false;
  loadingGrid = false;
  savingEntry = false;
  savingEdit = false;

  entryPondId = '';
  entryFarmerId = '';
  entryAmount: number | null = null;
  entryFarmers: GroupMember[] = [];

  gridPondId = '';
  transactions: CapitalTransactionRow[] = [];

  editingTransactionId: string | null = null;
  editingAmount: number | null = null;

  customMessage = '';
  isErrorMessage = false;
  readonly isReadOnly: boolean;

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
  ) {
    this.isReadOnly = (this.authService.getRole() || '').toLowerCase() === 'farmer';
  }

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

      if (this.ponds.length > 0) {
        this.entryPondId = this.ponds[0].id;
        this.gridPondId = this.ponds[0].id;
        await Promise.all([this.loadEntryFarmers(), this.loadGridTransactions()]);
      }
      this.setMessage('', false);
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to load groups.'), true);
    } finally {
      this.loadingGroups = false;
    }
  }

  async onEntryGroupChange(): Promise<void> {
    this.entryFarmerId = '';
    await this.loadEntryFarmers();
  }

  async loadEntryFarmers(): Promise<void> {
    const groupId = this.getGroupIdForPond(this.entryPondId);
    if (!groupId) {
      this.entryFarmers = [];
      return;
    }

    this.loadingFarmers = true;
    try {
      const data = await firstValueFrom(this.apiService.groups.members(groupId));
      this.entryFarmers = Array.isArray(data) ? data : [];
      if (!this.entryFarmers.some(m => m.userId === this.entryFarmerId)) {
        this.entryFarmerId = this.entryFarmers[0]?.userId ?? '';
      }
      this.setMessage('', false);
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to load farmers for selected group.'), true);
    } finally {
      this.loadingFarmers = false;
    }
  }

  async saveContributionEntry(): Promise<void> {
    if (this.isReadOnly) {
      return;
    }

    const groupId = this.getGroupIdForPond(this.entryPondId);
    if (!groupId || !this.entryFarmerId || this.entryAmount === null) {
      this.setMessage('Please select pond, farmer, and amount.', true);
      return;
    }

    if (this.entryAmount <= 0) {
      this.setMessage('Contribution amount must be greater than zero.', true);
      return;
    }

    this.savingEntry = true;
    try {
      await firstValueFrom(
        this.apiService.groups.recordContribution(groupId, this.entryFarmerId, this.entryAmount),
      );
      this.entryAmount = null;
      this.setMessage('Contribution saved successfully.', false);

      if (this.gridPondId === this.entryPondId) {
        await this.loadGridTransactions();
      }
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to save contribution.'), true);
    } finally {
      this.savingEntry = false;
    }
  }

  async onGridGroupChange(): Promise<void> {
    await this.loadGridTransactions();
  }

  clearGridGroupSelection(): void {
    this.gridPondId = '';
    this.transactions = [];
    this.editingTransactionId = null;
    this.editingAmount = null;
  }

  async loadGridTransactions(): Promise<void> {
    const groupId = this.getGroupIdForPond(this.gridPondId);
    if (!groupId) {
      this.transactions = [];
      return;
    }

    this.loadingGrid = true;
    this.editingTransactionId = null;
    this.editingAmount = null;
    try {
      const data = await firstValueFrom(this.apiService.groups.capitalTransactions(groupId));
      this.transactions = Array.isArray(data) ? data : [];
      this.setMessage('', false);
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to load contribution grid.'), true);
    } finally {
      this.loadingGrid = false;
    }
  }

  startEdit(row: CapitalTransactionRow): void {
    if (this.isReadOnly) {
      return;
    }

    this.editingTransactionId = row.id;
    this.editingAmount = Number(row.amount);
  }

  cancelEdit(): void {
    this.editingTransactionId = null;
    this.editingAmount = null;
  }

  async saveEditedAmount(row: CapitalTransactionRow): Promise<void> {
    if (this.isReadOnly) {
      return;
    }

    const groupId = this.getGroupIdForPond(this.gridPondId);
    if (!groupId || this.editingAmount === null) {
      return;
    }

    if (this.editingAmount < 0) {
      this.setMessage('Amount cannot be negative.', true);
      return;
    }

    this.savingEdit = true;
    try {
      await firstValueFrom(
        this.apiService.groups.updateCapitalTransactionAmount(groupId, row.id, this.editingAmount),
      );
      this.setMessage('Contribution amount updated successfully.', false);
      await this.loadGridTransactions();
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to update contribution amount.'), true);
    } finally {
      this.savingEdit = false;
      this.cancelEdit();
    }
  }

  private getGroupIdForPond(pondId: string): string {
    const pond = this.ponds.find(p => p.id === pondId);
    return pond?.groupId ?? '';
  }

  private setMessage(message: string, isError: boolean): void {
    this.customMessage = message;
    this.isErrorMessage = isError;
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
