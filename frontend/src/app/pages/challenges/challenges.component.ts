import { Component, OnInit, OnDestroy } from '@angular/core';
import { Challenge, Instance, Metadata } from 'src/app/model';
import { HelperService } from 'src/app/services/helper.service';
import { DataService } from 'src/app/services/data.service';

@Component({
    selector: 'app-challenges',
    templateUrl: './challenges.component.html',
    styleUrls: ['./challenges.component.less'],
    standalone: false
})
export class ChallengesComponent implements OnInit, OnDestroy {
  public hideSolved = false;
  public filterCategory = '';
  public filterDifficulty = '';
  public instance: Instance | null = null;

  private _metadata = new Metadata();
  private _challenges: Challenge[] = [];
  private instanceUpdateSubscription = this.dataService.instance.subscribe(instance => this.handleInstanceUpdate(instance));
  private updateMetadataSubscription = this.dataService.metadata.subscribe(metadata => this.handleMetadataUpdate(metadata));
  private updateChallengesSubscription = this.dataService.challenges.subscribe(challenges => this.handleChallengesUpdate(challenges));

  constructor(
    private dataService: DataService,
    public helpers: HelperService
  ) {}

  ngOnInit(): void {
    this.hideSolved = localStorage.getItem('hideSolved') === 'true';
  }

  ngOnDestroy(): void {
    this.updateChallengesSubscription.unsubscribe();
    this.updateMetadataSubscription.unsubscribe();
    this.instanceUpdateSubscription.unsubscribe();
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

  getChallenges(category: string, includeSolved: boolean = false): Challenge[] {
    return (
      this._challenges.filter(c => c.categories.length != 0 && c.categories[0] == category)
        // filter by difficulty
        .filter(c => this.filterDifficulty === '' || this.filterDifficulty === c.difficulty)
        // filter by category
        .filter(c => this.filterCategory === '' || c.categories.includes(this.filterCategory))
    );
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
    const numChallenges = this.getChallenges(category, true).length;
    const playerSolves = 0;
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
