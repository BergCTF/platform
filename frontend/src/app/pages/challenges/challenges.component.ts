import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Challenge, Instance, Metadata, Solve, Team } from 'src/app/model';
import { HelperService } from 'src/app/services/helper.service';
import { DataService } from 'src/app/services/data.service';
import { PrettyDateComponent } from '../../widgets/pretty-date/pretty-date.component';
import { ChallengeStatusComponent } from '../../widgets/challenge-status/challenge-status.component';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { RouterLink } from '@angular/router';

@Component({
    selector: 'app-challenges',
    templateUrl: './challenges.component.html',
    styleUrls: ['./challenges.component.less'],
    imports: [RouterLink, PrettyDateComponent, ChallengeStatusComponent, FormsModule]
})
export class ChallengesComponent implements OnInit, OnDestroy {
  public hideSolved = false;
  public filterCategory = '';
  public filterDifficulty = '';
  public instance: Instance | null = null;

  private _metadata = new Metadata();
  private _challenges: Challenge[] = [];
  private _solves: Solve[] = [];
  private _teams: Team[] = [];
  private instancesSubscription: Subscription | null = null;
  private metadataSubscription: Subscription | null = null;
  private challengesSubscription: Subscription | null = null;
  private teamsSubscription: Subscription | null = null;
  private solvesSubscription: Subscription | null = null;

  constructor(
    public dataService: DataService,
    public helpers: HelperService,
    private changeDetectorRef: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.hideSolved = localStorage.getItem('hideSolved') === 'true';
    this.instancesSubscription = this.dataService.instance.subscribe(instance => {
      this.instance = instance;
      this.changeDetectorRef.detectChanges();
    });
    this.metadataSubscription = this.dataService.metadata.subscribe(metadata => {
      this._metadata = metadata;
    });
    this.challengesSubscription = this.dataService.challenges.subscribe(challenges => {
      this._challenges = challenges;
      this.changeDetectorRef.detectChanges();
    });
    this.teamsSubscription = this.dataService.teams.subscribe(teams => {
      this._teams = teams;
      this.changeDetectorRef.detectChanges();
    })
    this.solvesSubscription = this.dataService.solves.subscribe(solves => {
      this._solves = solves;
      this.changeDetectorRef.detectChanges();
    });
  }

  ngOnDestroy(): void {
    this.challengesSubscription?.unsubscribe();
    this.metadataSubscription?.unsubscribe();
    this.instancesSubscription?.unsubscribe();
    this.teamsSubscription?.unsubscribe();
    this.solvesSubscription?.unsubscribe();
  }

  getChallenges(category: string, includeSolved: boolean = false): Challenge[] {
    return this._challenges.filter(c => c.categories.length != 0 && c.categories[0] == category)
        // hide solved
        .filter(c => includeSolved || !(this.hideSolved && this.hasSolvedChallenge(c.name)))
        // filter by difficulty
        .filter(c => this.filterDifficulty === '' || this.filterDifficulty === c.difficulty)
        // filter by category
        .filter(c => this.filterCategory === '' || c.categories.includes(this.filterCategory));
  }

  hasSolvedChallenge(challengeName: string): boolean {
    let team = this._teams.find(t => t.players.includes(this.dataService.currentPlayerId));
    if (team) {
      return this._solves.find(s => s.challengeName == challengeName && team.players.includes(s.playerId)) != undefined;
    } else {
      return this._solves.find(s => s.challengeName == challengeName && s.playerId == this.dataService.currentPlayerId) != undefined;
    }
  }

  getStart(): Date {
    return new Date(this._metadata.start);
  }

  getEnd(): Date {
    return new Date(this._metadata.end);
  }

  now(): Date {
    return new Date();
  }

  startChallengeInstance(challenge: string) {
    this.dataService.startInstance(challenge);
  }

  stopChallengeInstance() {
    this.dataService.stopInstance();
  }

  getAllCategories() {
    return [...new Set(this._challenges.flatMap(c => c.categories.length == 0 ? ['uncategorized'] : c.categories))].sort();
  }

  getAllDifficulties() {
    return [...new Set(this._challenges.map(c => c.difficulty))].sort();
  }

  getPrimaryCategories() {
    return [...new Set(this._challenges.map(c => c.categories.length == 0 ? 'uncategorized' : c.categories[0]))].sort();
  }

  getCategorySolves(category: string) {
    let challengeNames = this.getChallenges(category, true).map(c => c.name);
    const numChallenges = challengeNames.length;
    const playerSolves = this._solves.filter(s => s.playerId == this.dataService.currentPlayerId && challengeNames.includes(s.challengeName)).length;
    return `(${playerSolves}/${numChallenges})`;
  }

  updateHideState() {
    localStorage.setItem('hideSolved', Boolean(this.hideSolved).toString());
  }
}
