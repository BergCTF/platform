import { Component, OnDestroy } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Router } from '@angular/router';
import { DataService } from 'src/app/services/data.service';
import { AsyncPipe } from '@angular/common';

@Component({
    selector: 'app-profile-settings',
    templateUrl: './profile-settings.component.html',
    styleUrls: ['./profile-settings.component.less'],
    imports: [AsyncPipe]
})
export class ProfileSettingsComponent implements OnDestroy {

  apiKey: string | null = null;

  constructor(
    public apiService: ApiService,
    public dataService: DataService,
    private router: Router
  ) {}

  ngOnDestroy(): void {
    this.apiKey = null;
  }

  deleteAccount() {
    this.apiService.deleteCurrentPlayer().subscribe(() => {
      this.dataService.logout(true);
      this.router.navigate(['/']);
    });
  }

  resetAPIKey() {
    this.apiService.resetApiKey().subscribe(key => {
      this.apiKey = key;
      this.dataService.refreshCurrentPlayer();
    });
  }

  getHost(): string {
    return window.location.host;
  }
}
