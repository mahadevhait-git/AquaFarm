import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NavigationComponent } from './components/navigation/navigation.component';
import { AuthService } from './services/auth.service';
import { GlobalBengaliKeyboardComponent } from './components/global-bengali-keyboard/global-bengali-keyboard.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NavigationComponent, GlobalBengaliKeyboardComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  constructor(private authService: AuthService) {}

  get isAuthenticated(): boolean {
    return this.authService.isAuthenticated();
  }
}
