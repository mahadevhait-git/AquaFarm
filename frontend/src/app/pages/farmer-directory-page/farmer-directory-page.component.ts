import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../services/api.service';

type DirectoryUser = {
  id: string;
  userName: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  role: string;
  isActive: boolean;
  createdAt: string;
};

type AssociatedPond = {
  id: string;
  name: string;
  location?: string | null;
  ownerName: string;
  groupName?: string | null;
  associationType: string;
  createdAt: string;
};

type SortColumn = 'name' | 'userName' | 'email' | 'phoneNumber' | 'createdAt';
type SortDirection = 'asc' | 'desc';

@Component({
  selector: 'app-farmer-directory-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './farmer-directory-page.component.html',
})
export class FarmerDirectoryPageComponent implements OnInit {
  rows: DirectoryUser[] = [];
  loading = true;
  errorMessage = '';
  searchText = '';
  sortColumn: SortColumn = 'name';
  sortDirection: SortDirection = 'asc';

  selectedFarmer: DirectoryUser | null = null;
  associatedPonds: AssociatedPond[] = [];
  loadingAssociatedPonds = false;
  associatedPondsError = '';
  updatingStatusUserId: string | null = null;

  constructor(private apiService: ApiService) {}

  ngOnInit(): void {
    this.loadFarmers();
  }

  get filteredAndSortedRows(): DirectoryUser[] {
    const query = this.searchText.trim().toLowerCase();
    const filtered = query
      ? this.rows.filter(row => {
        const name = `${row.firstName} ${row.lastName}`.toLowerCase();
        return name.includes(query)
          || row.userName.toLowerCase().includes(query)
          || row.email.toLowerCase().includes(query)
          || row.phoneNumber.toLowerCase().includes(query);
      })
      : [...this.rows];

    const direction = this.sortDirection === 'asc' ? 1 : -1;
    filtered.sort((a, b) => {
      let left = '';
      let right = '';

      if (this.sortColumn === 'name') {
        left = `${a.firstName} ${a.lastName}`.toLowerCase();
        right = `${b.firstName} ${b.lastName}`.toLowerCase();
      } else if (this.sortColumn === 'createdAt') {
        left = new Date(a.createdAt).toISOString();
        right = new Date(b.createdAt).toISOString();
      } else {
        left = String(a[this.sortColumn] ?? '').toLowerCase();
        right = String(b[this.sortColumn] ?? '').toLowerCase();
      }

      if (left < right) {
        return -1 * direction;
      }
      if (left > right) {
        return 1 * direction;
      }
      return 0;
    });

    return filtered;
  }

  toggleSort(column: SortColumn): void {
    if (this.sortColumn === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
      return;
    }

    this.sortColumn = column;
    this.sortDirection = 'asc';
  }

  getSortIndicator(column: SortColumn): string {
    if (this.sortColumn !== column) {
      return '';
    }
    return this.sortDirection === 'asc' ? '^' : 'v';
  }

  async toggleFarmerPonds(row: DirectoryUser): Promise<void> {
    if (this.selectedFarmer?.id === row.id) {
      this.selectedFarmer = null;
      this.associatedPonds = [];
      this.associatedPondsError = '';
      return;
    }

    this.selectedFarmer = row;
    this.loadingAssociatedPonds = true;
    this.associatedPondsError = '';

    try {
      const response = await firstValueFrom(this.apiService.adminUsers.associatedPonds(row.id));
      this.associatedPonds = Array.isArray(response) ? response : [];
    } catch (error) {
      this.associatedPonds = [];
      this.associatedPondsError = this.getErrorMessage(error, 'Failed to load associated ponds.');
    } finally {
      this.loadingAssociatedPonds = false;
    }
  }

  async setUserStatus(row: DirectoryUser, isActive: boolean): Promise<void> {
    if (this.updatingStatusUserId) {
      return;
    }

    this.updatingStatusUserId = row.id;
    this.errorMessage = '';
    try {
      await firstValueFrom(this.apiService.adminUsers.updateStatus(row.id, isActive));
      row.isActive = isActive;
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to update status.');
    } finally {
      this.updatingStatusUserId = null;
    }
  }

  private async loadFarmers(): Promise<void> {
    this.loading = true;
    this.errorMessage = '';
    try {
      const response = await firstValueFrom(this.apiService.adminUsers.farmers());
      this.rows = Array.isArray(response) ? response : [];
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load farmers.');
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
      if (error.status === 403) {
        return 'Only admin can access this page.';
      }
    }
    return fallback;
  }
}
