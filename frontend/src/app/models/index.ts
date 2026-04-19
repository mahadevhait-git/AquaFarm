export enum UserRole {
  Admin = 'Admin',
  Farmer = 'Farmer',
  GroupManager = 'GroupManager',
}

export enum TransactionType {
  Investment = 'Investment',
  Expense = 'Expense',
  Revenue = 'Revenue',
  GroupTransaction = 'GroupTransaction',
  LoanDisbursement = 'LoanDisbursement',
  LoanRepayment = 'LoanRepayment',
}

export enum InterestType {
  Simple = 'Simple',
  Compound = 'Compound',
}

export interface User {
  id: string;
  userName: string;
  email: string;
  role: UserRole;
  createdAt: Date;
}

export interface Pond {
  id: string;
  name: string;
  location?: string;
  ownerId: string;
  groupId?: string;
  createdAt: Date;
}

export interface Group {
  id: string;
  name: string;
  description?: string;
  managerId?: string;
  managerName?: string;
  memberCount?: number;
  farmerCount?: number;
  pondCount?: number;
  createdAt: Date;
}

export interface GroupMember {
  userId: string;
  name: string;
  phoneNumber: string;
  email: string;
  pondCount: number;
  joinedAt: Date;
}

export interface GroupMemberCandidate {
  userId: string;
  name: string;
  phoneNumber: string;
  email: string;
}

export interface Transaction {
  id: string;
  type: TransactionType;
  category: string;
  amount: number;
  date: Date;
  pondId?: string;
  groupId?: string;
  createdById: string;
  notes?: string;
}

export interface Expense {
  id: string;
  pondId: string;
  pondName: string;
  pondLocation?: string;
  amount: number;
  purpose: string;
  expenseDate: Date;
  billFileName?: string;
  createdAt: Date;
}

export interface PondBill {
  id: string;
  pondId: string;
  pondName: string;
  fileName: string;
  uploadedAt: Date;
}

export interface Loan {
  id: string;
  lenderId: string;
  borrowerId: string;
  principalAmount: number;
  interestRate: number;
  interestType: InterestType;
  termMonths: number;
  outstandingBalance: number;
  isClosed: boolean;
  startDate: Date;
  createdAt: Date;
}

export interface LoanSummary {
  id: string;
  principalAmount: number;
  outstandingBalance: number;
  accruedInterest: number;
  interestType: InterestType;
  startDate: Date;
  termMonths: number;
  isClosed: boolean;
}

export interface AuthResponse {
  token: string;
  role: string;
}
