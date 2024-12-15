import { Component, OnDestroy, OnInit } from '@angular/core';
import { DataService } from './services/data.service';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { CurrentPlayer } from './model';
import { AsyncPipe } from '@angular/common';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.less'],
    imports: [RouterLink, RouterLinkActive, RouterOutlet, AsyncPipe]
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'Berg Frontend';
  currentPlayer = new CurrentPlayer();
  private updateCurrentPlayerSubscription: Subscription | undefined;

  constructor(
    public dataService: DataService,
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
