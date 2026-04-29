import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-global-bengali-keyboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './global-bengali-keyboard.component.html',
})
export class GlobalBengaliKeyboardComponent {
  visible = false;
  minimized = true;
  private activeInput: HTMLInputElement | HTMLTextAreaElement | null = null;

  readonly rows: string[][] = [
    ['অ', 'আ', 'ই', 'ঈ', 'উ', 'ঊ', 'এ', 'ঐ', 'ও', 'ঔ'],
    ['ক', 'খ', 'গ', 'ঘ', 'ঙ', 'চ', 'ছ', 'জ', 'ঝ', 'ঞ'],
    ['ট', 'ঠ', 'ড', 'ঢ', 'ণ', 'ত', 'থ', 'দ', 'ধ', 'ন'],
    ['প', 'ফ', 'ব', 'ভ', 'ম', 'য', 'র', 'ল', 'শ', 'ষ', 'স', 'হ'],
    ['া', 'ি', 'ী', 'ু', 'ূ', 'ে', 'ৈ', 'ো', 'ৌ', '্', 'ং', 'ঃ', 'ঁ'],
  ];

  @HostListener('document:focusin', ['$event'])
  onFocusIn(event: FocusEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target) {
      return;
    }

    const isInput = target instanceof HTMLInputElement && this.isSupportedInputType(target.type);
    const isTextArea = target instanceof HTMLTextAreaElement;
    if (!isInput && !isTextArea) {
      return;
    }

    this.activeInput = target as HTMLInputElement | HTMLTextAreaElement;
    this.visible = true;
  }

  insertChar(char: string): void {
    if (!this.activeInput) {
      return;
    }

    const input = this.activeInput;
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? start;
    input.value = `${input.value.slice(0, start)}${char}${input.value.slice(end)}`;
    input.selectionStart = input.selectionEnd = start + char.length;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.focus();
  }

  backspace(): void {
    if (!this.activeInput) {
      return;
    }

    const input = this.activeInput;
    const start = input.selectionStart ?? input.value.length;
    const end = input.selectionEnd ?? start;

    if (start !== end) {
      input.value = `${input.value.slice(0, start)}${input.value.slice(end)}`;
      input.selectionStart = input.selectionEnd = start;
    } else if (start > 0) {
      input.value = `${input.value.slice(0, start - 1)}${input.value.slice(end)}`;
      input.selectionStart = input.selectionEnd = start - 1;
    }

    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.focus();
  }

  hideKeyboard(): void {
    this.minimized = true;
  }

  showKeyboard(): void {
    this.minimized = false;
  }

  private isSupportedInputType(type: string): boolean {
    const supported = ['text', 'search', 'email', 'tel', 'url'];
    return supported.includes((type || '').toLowerCase());
  }
}
