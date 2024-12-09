import { Component, OnInit, OnDestroy } from '@angular/core';
import { Challenge, Instance, Metadata, Solve } from 'src/app/model';
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
  private instanceUpdateSubscription: Subscription | undefined;
  private updateMetadataSubscription: Subscription | undefined;
  private updateChallengesSubscription: Subscription | undefined;
  private updateSolvesSubscription: Subscription | undefined;

  constructor(
    public dataService: DataService,
    public helpers: HelperService
  ) {}

  ngOnInit(): void {
    this.hideSolved = localStorage.getItem('hideSolved') === 'true';
    this.instanceUpdateSubscription = this.dataService.instance.subscribe(instance => this.handleInstanceUpdate(instance));
    this.updateMetadataSubscription = this.dataService.metadata.subscribe(metadata => this.handleMetadataUpdate(metadata));
    this.updateChallengesSubscription = this.dataService.challenges.subscribe(challenges => this.handleChallengesUpdate(challenges));
    this.updateSolvesSubscription = this.dataService.solves.subscribe(solves => this.handleSolvesUpdate(solves));
  }

  ngOnDestroy(): void {
    this.updateChallengesSubscription?.unsubscribe();
    this.updateMetadataSubscription?.unsubscribe();
    this.instanceUpdateSubscription?.unsubscribe();
    this.updateSolvesSubscription?.unsubscribe();
  }

  private handleChallengesUpdate(challenges: Challenge[]) {
    this._challenges = challenges;
  }

  private handleInstanceUpdate(instance: Instance | null) {
    this.instance = instance;
  }

  private handleMetadataUpdate(metadata: Metadata) {
    this._metadata = metadata;
  }

  private handleSolvesUpdate(solves: Solve[]) {
    this._solves = solves;
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
    return this._solves.find(s => s.challengeName == challengeName && s.playerId == this.dataService.currentPlayerId) !== undefined;
  }

  getStart(): Date {
    return new Date(this._metadata.start);
  }

  getEnd(): Date {
    return new Date(this._metadata.end);
  }

  getServerTime(): Date {
    return new Date(this._metadata.serverTime);
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

  copyToClipboard(element: HTMLElement) {
    navigator.clipboard
      .writeText(element.innerText)
      .then()
      .catch(e => console.error(e));
  }
}
