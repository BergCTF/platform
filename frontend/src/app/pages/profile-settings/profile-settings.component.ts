import { Component } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Router } from '@angular/router';

@Component({
    selector: 'app-profile-settings',
    templateUrl: './profile-settings.component.html',
    styleUrls: ['./profile-settings.component.less'],
    standalone: false
})
export class ProfileSettingsComponent {
  constructor(
    public apiService: ApiService,
    private router: Router
  ) {}

  deleteAccount() {
    this.apiService.deleteCurrentPlayer().subscribe(() => {
      this.router.navigate(['/']);
    });
  }
}
