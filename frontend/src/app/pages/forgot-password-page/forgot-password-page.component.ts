import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

@Component({
  selector: 'app-forgot-password-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, I18nPipe],
  templateUrl: './forgot-password-page.component.html',
})
export class ForgotPasswordPageComponent {
  email = '';
  otp = '';
  newPassword = '';
  confirmPassword = '';
  otpSent = false;
  errorMessage = '';
  successMessage = '';
  showNewPassword = false;
  showConfirmPassword = false;
  loading = false;
  resendCooldownSeconds = 0;
  private resendIntervalId: number | null = null;

  constructor(private apiService: ApiService) {}

  async requestOtp(event: Event): Promise<void> {
    event.preventDefault();
    this.errorMessage = '';
    this.successMessage = '';
    this.loading = true;

    try {
      await firstValueFrom(this.apiService.auth.requestForgotPasswordOtp(this.email));
      this.otpSent = true;
      this.successMessage = 'OTP sent to your email.';
      this.startResendCooldown();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error);
    } finally {
      this.loading = false;
    }
  }

  async handleReset(event: Event): Promise<void> {
    event.preventDefault();
    this.errorMessage = '';
    this.successMessage = '';

    if (this.newPassword !== this.confirmPassword) {
      this.errorMessage = 'Passwords do not match.';
      return;
    }

    this.loading = true;
    try {
      await firstValueFrom(this.apiService.auth.resetForgotPassword(this.email, this.otp, this.newPassword));
      this.successMessage = 'Password reset successfully.';
      this.otpSent = false;
      this.clearResendCooldown();
      this.otp = '';
      this.newPassword = '';
      this.confirmPassword = '';
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

    return 'Operation failed. Please try again.';
  }

  async resendOtp(): Promise<void> {
    if (this.loading || this.resendCooldownSeconds > 0) {
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.loading = true;

    try {
      await firstValueFrom(this.apiService.auth.requestForgotPasswordOtp(this.email));
      this.successMessage = 'OTP resent to your email.';
      this.startResendCooldown();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error);
    } finally {
      this.loading = false;
    }
  }

  private startResendCooldown(): void {
    this.clearResendCooldown();
    this.resendCooldownSeconds = 30;

    this.resendIntervalId = window.setInterval(() => {
      this.resendCooldownSeconds -= 1;
      if (this.resendCooldownSeconds <= 0) {
        this.clearResendCooldown();
      }
    }, 1000);
  }

  private clearResendCooldown(): void {
    if (this.resendIntervalId !== null) {
      window.clearInterval(this.resendIntervalId);
      this.resendIntervalId = null;
    }
    if (this.resendCooldownSeconds < 0) {
      this.resendCooldownSeconds = 0;
    }
  }
}
