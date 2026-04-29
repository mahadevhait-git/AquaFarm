import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { I18nPipe } from '../../pipes/i18n.pipe';
import { BengaliKeyboardComponent } from '../../components/bengali-keyboard/bengali-keyboard.component';

@Component({
  selector: 'app-register-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, I18nPipe, BengaliKeyboardComponent],
  templateUrl: './register-page.component.html',
})
export class RegisterPageComponent {
  step = 1;
  selectedRole: 'Farmer' | 'GroupManager' | '' = '';
  firstName = '';
  lastName = '';
  address = '';
  email = '';
  phoneNumber = '';
  password = '';
  errorMessage = '';
  showPassword = false;
  loading = false;
  showBnKeyboard = false;

  constructor(private apiService: ApiService, private authService: AuthService, private router: Router) {}

  selectRole(role: 'Farmer' | 'GroupManager'): void {
    this.selectedRole = role;
    this.step = 2;
    this.errorMessage = '';
  }

  goBackToRoleSelection(): void {
    this.step = 1;
    this.errorMessage = '';
  }

  async handleRegister(event: Event): Promise<void> {
    event.preventDefault();
    this.errorMessage = '';
    this.loading = true;

    try {
      if (!this.selectedRole) {
        this.errorMessage = 'Please select a role first.';
        this.loading = false;
        return;
      }

      const response = await firstValueFrom(
        this.apiService.auth.register(
          this.firstName,
          this.lastName,
          this.address,
          this.email,
          this.phoneNumber,
          this.password,
          this.selectedRole,
        ),
      );

      if (response?.token) {
        this.authService.setToken(response.token);
        this.authService.setRole(response.role);
        await this.router.navigate(['/dashboard']);
      } else {
        this.errorMessage = 'Registration failed. Please try again.';
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

    return 'Registration failed. Please check your details and try again.';
  }
}
