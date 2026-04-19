import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { LoginPageComponent } from './pages/login-page/login-page.component';
import { DashboardPageComponent } from './pages/dashboard-page/dashboard-page.component';
import { PondPageComponent } from './pages/pond-page/pond-page.component';
import { LoanPageComponent } from './pages/loan-page/loan-page.component';
import { InvestmentSummaryPageComponent } from './pages/investment-summary-page/investment-summary-page.component';
import { GroupPageComponent } from './pages/group-page/group-page.component';
import { ExpensePageComponent } from './pages/expense-page/expense-page.component';
import { RegisterPageComponent } from './pages/register-page/register-page.component';
import { ForgotPasswordPageComponent } from './pages/forgot-password-page/forgot-password-page.component';

export const routes: Routes = [
  { path: 'login', component: LoginPageComponent },
  { path: 'forgot-password', component: ForgotPasswordPageComponent },
  { path: 'register', component: RegisterPageComponent },
  { path: 'dashboard', component: DashboardPageComponent, canActivate: [authGuard] },
  { path: 'ponds', component: PondPageComponent, canActivate: [authGuard] },
  { path: 'loans', component: LoanPageComponent, canActivate: [authGuard] },
  { path: 'loans/summary', component: InvestmentSummaryPageComponent, canActivate: [authGuard] },
  { path: 'expenses', component: ExpensePageComponent, canActivate: [authGuard] },
  { path: 'groups', component: GroupPageComponent, canActivate: [authGuard] },
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: '**', redirectTo: '/login' },
];
