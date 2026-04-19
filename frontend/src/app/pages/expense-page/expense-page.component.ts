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

@Component({
  selector: 'app-expense-page',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe],
  templateUrl: './expense-page.component.html',
})
export class ExpensePageComponent {
  showEntryForm = false;
  selectedPondId = '';
  amount: number | null = null;
  purpose = '';
  expenseDate = this.toDateInputValue(new Date());
  selectedBillFiles: File[] = [];

  ponds: Pond[] = [];
  expenses: Expense[] = [];
  loading = false;
  saving = false;
  downloadingBillId: string | null = null;

  openBillsForExpenseId: string | null = null;
  expenseBills: ExpenseBillRow[] = [];
  loadingExpenseBills = false;
  uploadingExpenseBills = false;
  selectedExpenseBillFiles: File[] = [];
  downloadingExpenseBillId: string | null = null;

  message = '';
  isErrorMessage = false;

  constructor(private apiService: ApiService) {}

  async ngOnInit(): Promise<void> {
    await this.loadPonds();
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
    await this.loadExpenses();
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
    this.selectedExpenseBillFiles = [];
    await this.loadExpenseBills(expense.id);
  }

  closeExpenseBillsPanel(): void {
    this.openBillsForExpenseId = null;
    this.expenseBills = [];
    this.selectedExpenseBillFiles = [];
  }

  onExpenseBillsSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedExpenseBillFiles = input.files ? Array.from(input.files) : [];
  }

  clearExpenseBillFiles(inputElement: HTMLInputElement): void {
    this.selectedExpenseBillFiles = [];
    inputElement.value = '';
  }

  async loadExpenseBills(expenseId: string): Promise<void> {
    this.loadingExpenseBills = true;
    try {
      const data = await firstValueFrom(this.apiService.expenses.expenseBills(expenseId));
      this.expenseBills = Array.isArray(data) ? data : [];
      this.setMessage('', false);
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to load expense bills.'), true);
      this.expenseBills = [];
    } finally {
      this.loadingExpenseBills = false;
    }
  }

  async uploadExpenseBills(inputElement: HTMLInputElement): Promise<void> {
    if (!this.openBillsForExpenseId) {
      return;
    }

    if (this.selectedExpenseBillFiles.length === 0) {
      this.setMessage('Please choose at least one bill file.', true);
      return;
    }

    this.uploadingExpenseBills = true;
    try {
      for (const file of this.selectedExpenseBillFiles) {
        await firstValueFrom(this.apiService.expenses.uploadExpenseBill(this.openBillsForExpenseId, file));
      }
      this.clearExpenseBillFiles(inputElement);
      this.setMessage('Expense bills uploaded successfully.', false);
      await this.loadExpenseBills(this.openBillsForExpenseId);
    } catch (error) {
      this.setMessage(this.getErrorMessage(error, 'Failed to upload expense bills.'), true);
    } finally {
      this.uploadingExpenseBills = false;
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
