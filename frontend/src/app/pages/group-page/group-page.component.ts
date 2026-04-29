import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Group, GroupMember, GroupMemberCandidate, Pond } from '../../models';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { I18nPipe } from '../../pipes/i18n.pipe';
import { BengaliKeyboardComponent } from '../../components/bengali-keyboard/bengali-keyboard.component';

@Component({
  selector: 'app-group-page',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe, BengaliKeyboardComponent],
  templateUrl: './group-page.component.html',
})
export class GroupPageComponent implements OnInit {
  groups: Group[] = [];
  ponds: Pond[] = [];
  members: GroupMember[] = [];
  memberCandidates: GroupMemberCandidate[] = [];
  selectedGroup: Group | null = null;
  selectedPond: Pond | null = null;
  showMemberOptions = false;
  showAddMembersSection = true;
  showCreateFarmerSection = false;
  memberSearch = '';
  candidateSearch = '';
  loading = true;
  membersLoading = false;
  candidatesLoading = false;
  creatingFarmer = false;
  errorMessage = '';
  successMessage = '';
  generatedFarmerCredentials: { userName: string; password: string } | null = null;
  readonly isReadOnly: boolean;

  newFarmerFirstName = '';
  newFarmerLastName = '';
  newFarmerAddress = '';
  newFarmerEmail = '';
  newFarmerPhoneNumber = '';
  showFarmerAddressKeyboard = false;

  constructor(
    private apiService: ApiService,
    private authService: AuthService,
  ) {
    this.isReadOnly = (this.authService.getRole() || '').toLowerCase() === 'farmer';
  }

  ngOnInit(): void {
    this.loadGroups();
  }

  async loadGroups(): Promise<void> {
    try {
      const [groupData, pondData] = await Promise.all([
        firstValueFrom(this.apiService.groups.list()),
        firstValueFrom(this.apiService.ponds.list()),
      ]);
      this.groups = Array.isArray(groupData) ? groupData : [];
      this.ponds = Array.isArray(pondData) ? pondData : [];
      if (this.selectedGroup) {
        this.selectedGroup = this.groups.find(g => g.id === this.selectedGroup?.id) ?? null;
      }
      if (this.selectedPond) {
        this.selectedPond = this.ponds.find(p => p.id === this.selectedPond?.id) ?? null;
      }
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load ponds/groups.');
    } finally {
      this.loading = false;
    }
  }

  async selectPond(pond: Pond): Promise<void> {
    const ensuredPond = this.isReadOnly ? pond : await this.ensureGroupForPond(pond);
    if (!ensuredPond) {
      return;
    }

    const linkedGroup = this.groups.find(g => g.id === ensuredPond.groupId);
    if (!linkedGroup) {
      this.errorMessage = 'Unable to link this pond to a group.';
      return;
    }

    this.selectedPond = ensuredPond;
    this.selectedGroup = linkedGroup;
    this.showMemberOptions = true;
    this.showAddMembersSection = !this.isReadOnly;
    this.memberSearch = '';
    this.candidateSearch = '';
    await this.loadSelectedGroupDetails();
  }

  private async ensureGroupForPond(pond: Pond): Promise<Pond | null> {
    if (pond.groupId) {
      return pond;
    }

    try {
      const createdGroup = await firstValueFrom(this.apiService.groups.create({
        name: pond.name,
        description: pond.location ?? null,
      }));

      const linkedGroupId = createdGroup?.id;
      if (!linkedGroupId) {
        this.errorMessage = 'Could not create group for this pond.';
        return null;
      }

      const updatedPondPayload = {
        name: pond.name,
        location: pond.location ?? '',
        groupId: linkedGroupId,
      };
      await firstValueFrom(this.apiService.ponds.update(pond.id, updatedPondPayload));

      await this.loadGroups();
      const refreshedPond = this.ponds.find(p => p.id === pond.id) ?? null;
      this.successMessage = 'Pond is now ready for member management.';
      return refreshedPond;
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to prepare pond for member management.');
      return null;
    }
  }

  closeMemberOptions(): void {
    this.showMemberOptions = false;
    this.showAddMembersSection = false;
    this.selectedGroup = null;
    this.selectedPond = null;
    this.members = [];
    this.memberCandidates = [];
    this.memberSearch = '';
    this.candidateSearch = '';
    this.showCreateFarmerSection = false;
  }

  async reopenMemberOptions(): Promise<void> {
    this.showMemberOptions = true;
    this.showAddMembersSection = true;
    this.showCreateFarmerSection = false;
    await this.loadSelectedGroupDetails();
  }

  closeAddMembersSection(): void {
    this.showAddMembersSection = false;
  }

  reopenAddMembersSection(): void {
    this.showAddMembersSection = true;
  }

  openCreateFarmerSection(): void {
    this.showCreateFarmerSection = true;
  }

  closeCreateFarmerSection(): void {
    this.showCreateFarmerSection = false;
  }

  getMemberCountForPond(pond: Pond): number {
    const linkedGroup = this.groups.find(g => g.id === pond.groupId);
    return linkedGroup?.memberCount ?? 0;
  }

  async loadSelectedGroupDetails(): Promise<void> {
    await Promise.all([this.loadMembers(), this.loadMemberCandidates()]);
  }

  async loadMembers(): Promise<void> {
    if (!this.selectedGroup) {
      return;
    }

    this.membersLoading = true;
    try {
      const data = await firstValueFrom(this.apiService.groups.members(this.selectedGroup.id, this.memberSearch));
      this.members = Array.isArray(data) ? data : [];
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to load group members.');
    } finally {
      this.membersLoading = false;
    }
  }

  async loadMemberCandidates(): Promise<void> {
    if (!this.selectedGroup) {
      return;
    }

    this.candidatesLoading = true;
    try {
      const data = await firstValueFrom(
        this.apiService.groups.memberCandidates(this.selectedGroup.id, this.candidateSearch),
      );
      this.memberCandidates = Array.isArray(data) ? data : [];
      this.errorMessage = '';
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to search member candidates.');
    } finally {
      this.candidatesLoading = false;
    }
  }

  async addMember(candidate: GroupMemberCandidate): Promise<void> {
    if (this.isReadOnly) {
      return;
    }

    if (!this.selectedGroup) {
      return;
    }

    try {
      await firstValueFrom(this.apiService.groups.addMember(this.selectedGroup.id, candidate.userId));
      this.successMessage = 'Member added successfully.';
      await this.loadGroups();
      await this.loadSelectedGroupDetails();
      this.showMemberOptions = false;
      this.showAddMembersSection = false;
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to add member.');
    }
  }

  async createFarmer(): Promise<void> {
    if (this.isReadOnly) {
      return;
    }

    if (!this.selectedGroup) {
      return;
    }

    if (!this.newFarmerFirstName.trim()
      || !this.newFarmerLastName.trim()
      || !this.newFarmerAddress.trim()
      || !this.newFarmerEmail.trim()
      || !this.newFarmerPhoneNumber.trim()) {
      this.errorMessage = 'Please fill all farmer details.';
      this.successMessage = '';
      return;
    }

    this.creatingFarmer = true;
    try {
      const response = await firstValueFrom(this.apiService.groups.createFarmer(this.selectedGroup.id, {
        firstName: this.newFarmerFirstName.trim(),
        lastName: this.newFarmerLastName.trim(),
        address: this.newFarmerAddress.trim(),
        email: this.newFarmerEmail.trim(),
        phoneNumber: this.newFarmerPhoneNumber.trim(),
      }));

      this.generatedFarmerCredentials = {
        userName: String(response?.userName ?? ''),
        password: String(response?.autoGeneratedPassword ?? ''),
      };

      this.newFarmerFirstName = '';
      this.newFarmerLastName = '';
      this.newFarmerAddress = '';
      this.newFarmerEmail = '';
      this.newFarmerPhoneNumber = '';

      this.successMessage = 'Farmer created and added successfully. Share generated credentials securely.';
      this.errorMessage = '';
      await this.loadGroups();
      await this.loadSelectedGroupDetails();
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to create farmer.');
      this.successMessage = '';
    } finally {
      this.creatingFarmer = false;
    }
  }

  async removeMember(member: GroupMember): Promise<void> {
    if (this.isReadOnly) {
      return;
    }

    if (!this.selectedGroup) {
      return;
    }

    const shouldRemove = window.confirm(`Remove ${member.name} from this group?`);
    if (!shouldRemove) {
      return;
    }

    try {
      await firstValueFrom(this.apiService.groups.removeMember(this.selectedGroup.id, member.userId));
      this.successMessage = 'Member removed successfully.';
      await this.loadGroups();
      await this.loadSelectedGroupDetails();
      this.showMemberOptions = false;
    } catch (error) {
      this.errorMessage = this.getErrorMessage(error, 'Failed to remove member.');
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
