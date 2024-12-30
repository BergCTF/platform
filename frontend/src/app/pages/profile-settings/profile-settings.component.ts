import { Component, OnDestroy, OnInit } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Router } from '@angular/router';
import { DataService } from 'src/app/services/data.service';
import { Subscription } from 'rxjs';

@Component({
    selector: 'app-profile-settings',
    templateUrl: './profile-settings.component.html',
    styleUrls: ['./profile-settings.component.less'],
    imports: []
})
export class ProfileSettingsComponent implements OnInit, OnDestroy {

  private currentPlayerSubscription: Subscription | null = null;

  apiKey: string | null = null;
  apiKeyPlaceholder: string | null = null;
  currentPlayerId = '<uuid>';

  constructor(
    public apiService: ApiService,
    public dataService: DataService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.currentPlayerSubscription = this.dataService.currentPlayer.subscribe(currentPlayer => {
      this.apiKeyPlaceholder = currentPlayer?.apiKeyPlaceholder || null;
      this.currentPlayerId = currentPlayer?.id ?? '<uuid>';
    });
  }

  ngOnDestroy(): void {
    this.currentPlayerSubscription?.unsubscribe();
    this.apiKey = null;
    this.apiKeyPlaceholder = null;
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
      this.apiKeyPlaceholder = null;
      this.dataService.refreshCurrentPlayer();
    });
  }

  getHost(): string {
    return window.location.host;
  }
}
