import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-bengali-keyboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './bengali-keyboard.component.html',
})
export class BengaliKeyboardComponent {
  @Input() value = '';
  @Output() valueChange = new EventEmitter<string>();

  readonly rows: string[][] = [
    ['অ', 'আ', 'ই', 'ঈ', 'উ', 'ঊ', 'এ', 'ঐ', 'ও', 'ঔ'],
    ['ক', 'খ', 'গ', 'ঘ', 'ঙ', 'চ', 'ছ', 'জ', 'ঝ', 'ঞ'],
    ['ট', 'ঠ', 'ড', 'ঢ', 'ণ', 'ত', 'থ', 'দ', 'ধ', 'ন'],
    ['প', 'ফ', 'ব', 'ভ', 'ম', 'য', 'র', 'ল', 'শ', 'স', 'হ'],
    ['া', 'ি', 'ী', 'ু', 'ূ', 'ে', 'ৈ', 'ো', 'ৌ', '্', 'ং', 'ঃ', 'ঁ'],
  ];

  addChar(char: string): void {
    this.valueChange.emit(`${this.value}${char}`);
  }

  backspace(): void {
    if (!this.value) {
      return;
    }
    this.valueChange.emit(this.value.slice(0, -1));
  }

  space(): void {
    this.valueChange.emit(`${this.value} `);
  }

  clearAll(): void {
    this.valueChange.emit('');
  }
}
