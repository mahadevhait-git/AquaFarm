import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Pond } from '../../models';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

@Component({
  selector: 'app-pond-page',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe],
  templateUrl: './pond-page.component.html',
})
export class PondPageComponent implements OnInit {
  ponds: Pond[] = [];
  showForm = false;
  isEditMode = false;
  editingPondId = '';
  formData = { name: '', location: '' };
  loading = true;
  errorMessage = '';
  readonly isReadOnly: boolean;

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
  ) {
    this.isReadOnly = this.authService.getRole() === 'Farmer';
  }

  ngOnInit(): void {
    this.loadPonds();
  }

  async loadPonds(): Promise<void> {
    try {
      const data = await firstValueFrom(this.apiService.ponds.list());
      this.ponds = Array.isArray(data) ? data : [];
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load ponds.');
    } finally {
      this.loading = false;
    }
  }

  async handleCreatePond(event: Event): Promise<void> {
    event.preventDefault();
    if (this.isReadOnly) {
      return;
    }
    try {
      if (this.isEditMode && this.editingPondId) {
        await firstValueFrom(this.apiService.ponds.update(this.editingPondId, this.formData));
      } else {
        await firstValueFrom(this.apiService.ponds.create(this.formData));
      }

      this.resetForm();
      await this.loadPonds();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, this.isEditMode ? 'Failed to update pond.' : 'Failed to create pond.');
    }
  }

  startCreate(): void {
    if (this.isReadOnly) {
      return;
    }
    this.resetForm();
    this.showForm = true;
  }

  startEdit(pond: Pond): void {
    if (this.isReadOnly) {
      return;
    }
    this.showForm = true;
    this.isEditMode = true;
    this.editingPondId = pond.id;
    this.formData = {
      name: pond.name ?? '',
      location: pond.location ?? '',
    };
    this.errorMessage = '';
  }

  async deletePond(pond: Pond): Promise<void> {
    if (this.isReadOnly) {
      return;
    }
    const shouldDelete = window.confirm(`Delete pond "${pond.name}"?`);
    if (!shouldDelete) {
      return;
    }

    try {
      await firstValueFrom(this.apiService.ponds.delete(pond.id));
      await this.loadPonds();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to delete pond.');
    }
  }

  cancelForm(): void {
    this.resetForm();
  }

  private resetForm(): void {
    this.showForm = false;
    this.isEditMode = false;
    this.editingPondId = '';
    this.formData = { name: '', location: '' };
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
