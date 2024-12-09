import { Component, OnDestroy, OnInit } from '@angular/core';
import { DataService } from './services/data.service';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { CurrentPlayer, Player } from './model';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.less'],
    imports: [RouterLink, RouterLinkActive, RouterOutlet]
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'Berg Frontend';
  currentPlayer = new CurrentPlayer();
  private updateCurrentPlayerSubscription: Subscription | undefined;

  constructor(
    public dataService: DataService
  ) {}

  ngOnInit() {
    this.updateCurrentPlayerSubscription = this.dataService.currentPlayer.subscribe(currentPlayer => this.handleCurrentPlayerUpdate(currentPlayer));
  }

  ngOnDestroy(): void {
    this.updateCurrentPlayerSubscription?.unsubscribe();
  }

  private handleCurrentPlayerUpdate(player: CurrentPlayer) {
    this.currentPlayer = player;
  }
}
