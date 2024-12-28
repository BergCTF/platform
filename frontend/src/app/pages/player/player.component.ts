import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Challenge, Player, Solve } from 'src/app/model';
import { DataService } from 'src/app/services/data.service';
import { HelperService } from 'src/app/services/helper.service';
import { PrettyDateComponent } from '../../widgets/pretty-date/pretty-date.component';
import { Subscription } from 'rxjs';

@Component({
    selector: 'app-player',
    templateUrl: './player.component.html',
    styleUrls: ['./player.component.less'],
    imports: [RouterLink, PrettyDateComponent]
})
export class PlayerComponent implements OnInit, OnDestroy {

  private _uuid = '';
  private _players: Player[] = [];
  private _solves: Solve[] = [];
  private _challenges: Challenge[] = [];
  private routeParamsSubscription: Subscription | null = null;
  private playersSubscription: Subscription | null = null;
  private solvesSubscription: Subscription | null = null;
  private challengesSubscription: Subscription | null = null;

  player: Player | null = null;
  playerSolves: Solve[] = [];

  constructor(
    public dataService: DataService,
    public helpers: HelperService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.routeParamsSubscription = this.route.params.subscribe(params => {
      this._uuid = params['uuid'];
      this.handlePlayerUpdate();
    });
    this.playersSubscription = this.dataService.players.subscribe(players => {
      this._players = players;
      this.handlePlayerUpdate();
    });
    this.solvesSubscription = this.dataService.solves.subscribe(solves => {
      this._solves = solves;
      this.handlePlayerUpdate();
    });
    this.challengesSubscription = this.dataService.challenges.subscribe(challenges => this.handleChallengesUpdate(challenges));
  }

  ngOnDestroy(): void {
    this.routeParamsSubscription?.unsubscribe();
    this.playersSubscription?.unsubscribe();
    this.solvesSubscription?.unsubscribe();
    this.challengesSubscription?.unsubscribe();
  }

  private handlePlayerUpdate() {
    this.player = this._players.find(p => p.id == this._uuid) || null;
    this.playerSolves = this._solves.filter(s => s.playerId == this._uuid);
  }

  private handleChallengesUpdate(challenges: Challenge[]) {
    this._challenges = challenges;
  }

  getPrimaryCategories() {
    return [...new Set(this._challenges.map(c => c.categories.length == 0 ? 'uncategorized' : c.categories[0]))].sort();
  }

  getCategoryProgress(category: string) {
    let categoryChallenges = this._challenges.filter(c => c.categories.includes(category));
    let solvedChallenges = categoryChallenges.filter(challenge => this.playerSolves.some(solve => solve.challengeName === challenge.name));
    return Math.floor((solvedChallenges.length / categoryChallenges.length) * 100);
  }

  getBarColor(percentage: number) {
    const minHue = 240;
    const maxHue = 120;
    percentage /= 100;
    const colorString = `hsl(${percentage * (maxHue - minHue) + minHue},60%,${40 + 10 * percentage}%)`;
    return colorString;
  }
}
