import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { InterestType } from '../../models';
import { ApiService } from '../../services/api.service';
import { I18nPipe } from '../../pipes/i18n.pipe';

@Component({
  selector: 'app-loan-page',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe],
  templateUrl: './loan-page.component.html',
})
export class LoanPageComponent {
  showForm = false;
  formData = {
    borrowerId: '',
    principalAmount: '',
    interestRate: '',
    interestType: InterestType.Simple,
    termMonths: '',
  };
  loading = false;
  messageKey = '';
  isErrorMessage = false;
  readonly interestType = InterestType;

  constructor(private apiService: ApiService) {}

  async handleCreateLoan(event: Event): Promise<void> {
    event.preventDefault();
    this.loading = true;

    try {
      await firstValueFrom(
        this.apiService.loans.create({
          borrowerId: this.formData.borrowerId,
          principalAmount: Number(this.formData.principalAmount),
          interestRate: Number(this.formData.interestRate),
          interestType: this.formData.interestType,
          termMonths: Number(this.formData.termMonths),
        }),
      );
      this.messageKey = 'loan.success';
      this.isErrorMessage = false;
      this.formData = {
        borrowerId: '',
        principalAmount: '',
        interestRate: '',
        interestType: InterestType.Simple,
        termMonths: '',
      };
      this.showForm = false;
    } catch {
      this.messageKey = 'loan.failed';
      this.isErrorMessage = true;
    } finally {
      this.loading = false;
    }
  }
}
