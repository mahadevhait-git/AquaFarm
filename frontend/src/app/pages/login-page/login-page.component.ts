import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, I18nPipe],
  templateUrl: './login-page.component.html',
})
export class LoginPageComponent {
  phoneNumber = '';
  password = '';
  errorMessage = '';
  showPassword = false;
  loading = false;

  constructor(private apiService: ApiService, private authService: AuthService, private router: Router) {}

  async handleLogin(event: Event): Promise<void> {
    event.preventDefault();
    this.errorMessage = '';
    this.loading = true;

    try {
      const response = await firstValueFrom(this.apiService.auth.login(this.phoneNumber, this.password));
      if (response?.token) {
        this.authService.setToken(response.token);
        this.authService.setRole(response.role);
        await this.router.navigate(['/dashboard']);
      } else {
        this.errorMessage = 'Invalid phone number or password.';
      }
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error);
    } finally {
      this.loading = false;
    }
  }

  private getErrorMessage(error: unknown): string {
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

    return 'Login failed. Please try again.';
  }
}
