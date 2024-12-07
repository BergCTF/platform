import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Params } from '@angular/router';
import { Challenge, Player, Solve } from 'src/app/model';
import { DataService } from 'src/app/services/data.service';
import { HelperService } from 'src/app/services/helper.service';

@Component({
    selector: 'app-player',
    templateUrl: './player.component.html',
    styleUrls: ['./player.component.less'],
    standalone: false
})
export class PlayerComponent implements OnInit, OnDestroy {

  private _uuid = '';
  private _players: Player[] = [];
  private _solves: Solve[] = [];
  private _challenges: Challenge[] = [];
  private routeUpdateSubscription = this.route.params.subscribe(params => this.handleRouteUpdate(params));
  private playerUpdateSubscription = this.dataService.players.subscribe(players => this.handlePlayersUpdate(players));
  private solvesUpdateSubscription = this.dataService.solves.subscribe(solves => this.handleSolvesUpdate(solves));
  private challengesUpdateSubscription = this.dataService.challenges.subscribe(challenges => this.handleChallengesUpdate(challenges));

  player: Player | undefined = undefined;
  playerSolves: Solve[] = [];

  constructor(
    public dataService: DataService,
    public helpers: HelperService,
    private route: ActivatedRoute
  ) {}

  ngOnDestroy(): void {
    this.routeUpdateSubscription.unsubscribe();
    this.playerUpdateSubscription.unsubscribe();
    this.solvesUpdateSubscription.unsubscribe();
    this.challengesUpdateSubscription.unsubscribe();
  }

  ngOnInit(): void {
  }

  private handleRouteUpdate(params: Params) {
    this._uuid = params['uuid'];
    this.dataService.ensurePlayer(this._uuid);
    this.handlePlayerUpdate();
  }

  private handlePlayersUpdate(players: Player[]) {
    this._players = players;
    this.handlePlayerUpdate();
  }

  private handlePlayerUpdate() {
    this.player = this._players.find(p => p.id == this._uuid);
    this.playerSolves = this._solves.filter(s => s.playerId == this._uuid);
  }

  private handleSolvesUpdate(solves: Solve[]) {
    this._solves = solves;
  }

  private handleChallengesUpdate(challenges: Challenge[]) {
    this._challenges = challenges;
  }

  playerName() {
    if (this.player) {
      return this.player.name;
    }
    return 'Player not found';
  }

  lastSolve(): string {
    if (this.playerSolves) {
      return this.playerSolves.reduce((a, b) => a < b ? b : a).solvedAt;
    }
    return '';
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
