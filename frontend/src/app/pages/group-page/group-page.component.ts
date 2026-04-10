import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Group } from '../../models';
import { ApiService } from '../../services/api.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

@Component({
  selector: 'app-group-page',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe],
  templateUrl: './group-page.component.html',
})
export class GroupPageComponent implements OnInit {
  groups: Group[] = [];
  showForm = false;
  formData = { name: '', description: '' };
  loading = true;
  errorMessage = '';

  constructor(private apiService: ApiService) {}

  ngOnInit(): void {
    this.loadGroups();
  }

  async loadGroups(): Promise<void> {
    try {
      const data = await firstValueFrom(this.apiService.groups.list());
      this.groups = Array.isArray(data) ? data : [];
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load groups.');
    } finally {
      this.loading = false;
    }
  }

  async handleCreateGroup(event: Event): Promise<void> {
    event.preventDefault();
    try {
      await firstValueFrom(
        this.apiService.groups.create({
          name: this.formData.name,
          description: this.formData.description,
        }),
      );
      this.formData = { name: '', description: '' };
      this.showForm = false;
      await this.loadGroups();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to create group.');
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
}
