import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Pond } from '../../models';
import { ApiService } from '../../services/api.service';
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
  formData = { name: '', location: '', ownerId: '' };
  loading = true;
  errorKey = '';

  constructor(private apiService: ApiService) {}

  ngOnInit(): void {
    this.loadPonds();
  }

  async loadPonds(): Promise<void> {
    try {
      const data = await firstValueFrom(this.apiService.ponds.list());
      this.ponds = Array.isArray(data) ? data : [];
      this.errorKey = '';
    } catch {
      this.errorKey = 'pond.failedLoad';
    } finally {
      this.loading = false;
    }
  }

  async handleCreatePond(event: Event): Promise<void> {
    event.preventDefault();
    try {
      await firstValueFrom(this.apiService.ponds.create(this.formData));
      this.formData = { name: '', location: '', ownerId: '' };
      this.showForm = false;
      await this.loadPonds();
    } catch {
      this.errorKey = 'pond.failedCreate';
    }
  }
}
