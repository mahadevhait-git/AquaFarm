import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { Pond } from '../../models';
import { ApiService } from '../../services/api.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, I18nPipe],
  templateUrl: './dashboard-page.component.html',
})
export class DashboardPageComponent implements OnInit {
  ponds: Pond[] = [];
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
      this.errorKey = 'dashboard.failed';
    } finally {
      this.loading = false;
    }
  }
}
