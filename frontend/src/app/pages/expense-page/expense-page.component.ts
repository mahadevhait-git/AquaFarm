import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Expense, Pond } from '../../models';
import { ApiService } from '../../services/api.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

type ExpenseBillRow = {
  id: string;
  expenseId: string;
  fileName: string;
  uploadedAt: string;
  isLegacy?: boolean;
};

type BillPreview = {
  url: string;
  kind: 'image' | 'pdf' | 'other';
};

@Component({
  selector: 'app-expense-page',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe],
  templateUrl: './expense-page.component.html',
})
export class ExpensePageComponent {
  showEntryForm = false;
  selectedPondId = '';
  selectedPondTotalSpent: number | null = null;
  amount: number | null = null;
  purpose = '';
  expenseDate = this.toDateInputValue(new Date());
  selectedBillFiles: File[] = [];

  ponds: Pond[] = [];
  expenses: Expense[] = [];
  loading = false;
  saving = false;
  deletingExpenseId: string | null = null;
  downloadingBillId: string | null = null;

  openBillsForExpenseId: string | null = null;
  expenseBills: ExpenseBillRow[] = [];
  loadingExpenseBills = false;
  downloadingExpenseBillId: string | null = null;
  billPreviewByKey: Record<string, BillPreview> = {};
  loadingBillPreviewKeys = new Set<string>();
  largePreviewUrl: string | null = null;
  largePreviewFileName = '';
  largePreviewScale = 1;

  message = '';
  isErrorMessage = false;

  constructor(private apiService: ApiService) {}

  async ngOnInit(): Promise<void> {
    await this.loadPonds();
  }

  ngOnDestroy(): void {
    this.clearBillPreviews();
  }

  async loadPonds(): Promise<void> {
    try {
      const data = await firstValueFrom(this.apiService.ponds.list());
      this.ponds = Array.isArray(data) ? data : [];
      if (!this.selectedPondId && this.ponds.length > 0) {
        this.selectedPondId = this.ponds[0].id;
      }
      await this.loadExpenses();
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to load ponds.'), true);
    }
  }

  async loadExpenses(): Promise<void> {
    this.loading = true;
    try {
      const data = await firstValueFrom(this.apiService.expenses.list(this.selectedPondId || undefined));
      this.expenses = Array.isArray(data) ? data : [];
      if (this.openBillsForExpenseId && !this.expenses.some(e => e.id === this.openBillsForExpenseId)) {
        this.closeExpenseBillsPanel();
      }
      this.setMessage('', false);
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to load expenses.'), true);
      this.expenses = [];
    } finally {
      this.loading = false;
    }
  }

  async onPondSelectionChange(): Promise<void> {
    this.selectedPondTotalSpent = null;
    if (!this.selectedPondId) {
      this.closeExpenseBillsPanel();
      this.expenses = [];
      this.loading = false;
      return;
    }

    await this.loadExpenses();
  }

  showSelectedPondTotalSpent(): void {
    if (!this.selectedPondId) {
      this.setMessage('Please select a pond first.', true);
      this.selectedPondTotalSpent = null;
      return;
    }

    this.selectedPondTotalSpent = this.expenses.reduce((sum, expense) => sum + Number(expense.amount || 0), 0);
    this.setMessage('', false);
  }

  onBillFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedBillFiles = input.files ? Array.from(input.files) : [];
  }

  clearBillFile(inputElement: HTMLInputElement): void {
    this.selectedBillFiles = [];
    inputElement.value = '';
  }

  async saveExpense(): Promise<void> {
    if (!this.selectedPondId) {
      this.setMessage('Please select a pond for this expense.', true);
      return;
    }

    if (this.amount === null || this.amount <= 0) {
      this.setMessage('Expense amount must be greater than zero.', true);
      return;
    }

    if (!this.purpose.trim()) {
      this.setMessage('Expense purpose is required.', true);
      return;
    }

    if (!this.expenseDate) {
      this.setMessage('Expense date is required.', true);
      return;
    }

    this.saving = true;
    try {
      await firstValueFrom(
        this.apiService.expenses.create({
          pondId: this.selectedPondId,
          amount: this.amount,
          purpose: this.purpose.trim(),
          date: this.expenseDate,
          bills: this.selectedBillFiles,
        }),
      );
      this.amount = null;
      this.purpose = '';
      this.expenseDate = this.toDateInputValue(new Date());
      this.selectedBillFiles = [];
      this.showEntryForm = false;
      this.setMessage('Expense saved successfully.', false);
      await this.loadExpenses();
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to save expense.'), true);
    } finally {
      this.saving = false;
    }
  }

  async removeExpense(expense: Expense): Promise<void> {
    const confirmed = window.confirm('Are you sure you want to remove this expense?');
    if (!confirmed) {
      return;
    }

    this.deletingExpenseId = expense.id;
    try {
      await firstValueFrom(this.apiService.expenses.delete(expense.id));
      if (this.openBillsForExpenseId === expense.id) {
        this.closeExpenseBillsPanel();
      }
      this.setMessage('Expense removed successfully.', false);
      await this.loadExpenses();
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to remove expense.'), true);
    } finally {
      this.deletingExpenseId = null;
    }
  }

  async downloadBill(expense: Expense): Promise<void> {
    this.downloadingBillId = expense.id;
    try {
      const blob = await firstValueFrom(this.apiService.expenses.downloadBill(expense.id));
      this.downloadBlob(blob, expense.billFileName || 'expense-bill');
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to download bill attachment.'), true);
    } finally {
      this.downloadingBillId = null;
    }
  }

  async toggleExpenseBills(expense: Expense): Promise<void> {
    if (this.openBillsForExpenseId === expense.id) {
      this.closeExpenseBillsPanel();
      return;
    }

    this.openBillsForExpenseId = expense.id;
    await this.loadExpenseBills(expense.id);
  }

  closeExpenseBillsPanel(): void {
    this.clearBillPreviews();
    this.openBillsForExpenseId = null;
    this.expenseBills = [];
  }

  async loadExpenseBills(expenseId: string): Promise<void> {
    this.loadingExpenseBills = true;
    try {
      const data = await firstValueFrom(this.apiService.expenses.expenseBills(expenseId));
      this.expenseBills = Array.isArray(data) ? data : [];
      await this.loadBillPreviews(this.expenseBills);
      this.setMessage('', false);
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to load expense bills.'), true);
      this.expenseBills = [];
      this.clearBillPreviews();
    } finally {
      this.loadingExpenseBills = false;
    }
  }

  async downloadExpenseBill(bill: ExpenseBillRow): Promise<void> {
    this.downloadingExpenseBillId = bill.id || bill.expenseId;
    try {
      const blob = bill.isLegacy
        ? await firstValueFrom(this.apiService.expenses.downloadBill(bill.expenseId))
        : await firstValueFrom(this.apiService.expenses.downloadExpenseBill(bill.id));
      this.downloadBlob(blob, bill.fileName || 'expense-bill');
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to download expense bill.'), true);
    } finally {
      this.downloadingExpenseBillId = null;
    }
  }

  getBillPreview(bill: ExpenseBillRow): BillPreview | null {
    return this.billPreviewByKey[this.getBillKey(bill)] || null;
  }

  isBillPreviewLoading(bill: ExpenseBillRow): boolean {
    return this.loadingBillPreviewKeys.has(this.getBillKey(bill));
  }

  openLargePreview(previewUrl: string, fileName: string): void {
    this.largePreviewUrl = previewUrl;
    this.largePreviewFileName = fileName;
    this.largePreviewScale = 1;
  }

  closeLargePreview(): void {
    this.largePreviewUrl = null;
    this.largePreviewFileName = '';
    this.largePreviewScale = 1;
  }

  zoomInLargePreview(): void {
    this.largePreviewScale = Math.min(4, Number((this.largePreviewScale + 0.25).toFixed(2)));
  }

  zoomOutLargePreview(): void {
    this.largePreviewScale = Math.max(0.5, Number((this.largePreviewScale - 0.25).toFixed(2)));
  }

  private async loadBillPreviews(bills: ExpenseBillRow[]): Promise<void> {
    this.clearBillPreviews();
    if (bills.length === 0) {
      return;
    }

    await Promise.all(bills.map(async bill => {
      const key = this.getBillKey(bill);
      this.loadingBillPreviewKeys.add(key);

      try {
        const blob = bill.isLegacy
          ? await firstValueFrom(this.apiService.expenses.downloadBill(bill.expenseId))
          : await firstValueFrom(this.apiService.expenses.downloadExpenseBill(bill.id));
        const kind = this.getPreviewKind(blob.type, bill.fileName);
        const url = window.URL.createObjectURL(blob);
        this.billPreviewByKey[key] = { url, kind };
      } catch {
        // keep download-only behavior if preview fetch fails
      } finally {
        this.loadingBillPreviewKeys.delete(key);
      }
    }));
  }

  private clearBillPreviews(): void {
    Object.values(this.billPreviewByKey).forEach(preview => {
      window.URL.revokeObjectURL(preview.url);
    });
    this.billPreviewByKey = {};
    this.loadingBillPreviewKeys.clear();
  }

  private getBillKey(bill: ExpenseBillRow): string {
    return `${bill.isLegacy ? 'legacy' : 'bill'}:${bill.id}:${bill.expenseId}:${bill.fileName}`;
  }

  private getPreviewKind(mimeType: string, fileName: string): BillPreview['kind'] {
    const normalizedMime = (mimeType || '').toLowerCase();
    const normalizedName = (fileName || '').toLowerCase();
    if (normalizedMime.startsWith('image/') || /\.(jpg|jpeg|png|gif|webp|bmp|svg)$/.test(normalizedName)) {
      return 'image';
    }
    if (normalizedMime === 'application/pdf' || normalizedName.endsWith('.pdf')) {
      return 'pdf';
    }
    return 'other';
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    window.URL.revokeObjectURL(url);
  }

  private toDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private setMessage(message: string, isError: boolean): void {
    this.message = message;
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

      const validationErrors = error.error?.errors;
      if (validationErrors && typeof validationErrors === 'object') {
        const firstKey = Object.keys(validationErrors)[0];
        const firstMessages = firstKey ? validationErrors[firstKey] : null;
        if (Array.isArray(firstMessages) && firstMessages.length > 0) {
          return String(firstMessages[0]);
        }
      }

      if (error.status === 403) {
        return 'You do not have permission to save for this pond.';
      }

      if (error.status === 0) {
        return 'Server is unreachable. Please check if the backend is running.';
      }
    }

    return fallback;
  }
}
