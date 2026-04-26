import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { I18nService, AppLanguage } from '../../services/i18n.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

@Component({
  selector: 'app-navigation',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, RouterLinkActive, I18nPipe],
  templateUrl: './navigation.component.html',
})
export class NavigationComponent {
  constructor(
    private authService: AuthService,
    private router: Router,
    public i18nService: I18nService,
  ) {}

  get isAuthenticated(): boolean {
    return this.authService.isAuthenticated();
  }

  get currentLanguage(): AppLanguage {
    return this.i18nService.currentLanguage;
  }

  set currentLanguage(language: AppLanguage) {
    this.i18nService.setLanguage(language);
  }

  get currentUserName(): string {
    return this.authService.getUserName();
  }

  get currentUserRole(): string {
    return this.authService.getRole() || 'Unknown Role';
  }

  get isFarmer(): boolean {
    return (this.authService.getRole() || '').toLowerCase() === 'farmer';
  }

  get isAdmin(): boolean {
    return (this.authService.getRole() || '').toLowerCase() === 'admin';
  }

  logout(): void {
    this.authService.clearSession();
    this.router.navigate(['/login']);
  }
}
