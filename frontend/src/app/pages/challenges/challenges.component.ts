import { Component, OnInit, ElementRef, ViewChild, inject, ChangeDetectorRef, OnDestroy } from '@angular/core';
import { Location } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Challenge, Instance, Metadata, Solve } from 'src/app/model';
import { ActivatedRoute, Params } from '@angular/router';
import { HelperService } from 'src/app/services/helper.service';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { DataService } from 'src/app/services/data.service';

@Component({
  selector: 'app-challenges',
  templateUrl: './challenges.component.html',
  styleUrls: ['./challenges.component.less'],
})
export class ChallengesComponent implements OnInit, OnDestroy {
  @ViewChild('challengeModal') challengeModal!: ElementRef;
  private modalService = inject(NgbModal);

  public currentChallenge: Challenge | null = null;
  public descriptionHtml: SafeHtml = '';
  public flagValue = '';
  public flagErrorMessage = '';
  public isFlagSubmitting = false;
  public hideSolved = false;
  public filterCategory = '';
  public filterDifficulty = '';

  public instance: Instance | null = null;

  private _metadata = new Metadata();
  private _launchChallengeName = '';
  private _challenges: Challenge[] = [];
  private routeUpdateSubscription = this.route.params.subscribe(this.handleRouteUpdate);
  private instanceUpdateSubscription = this.dataService.instance.subscribe(this.handleInstanceUpdate);
  private updateChallengesSubscription = this.dataService.challenges.subscribe(this.handleUpdateChallenges);

  constructor(
    private dataService: DataService,
    private domSanitizer: DomSanitizer,
    private route: ActivatedRoute,
    public helpers: HelperService,
    private location: Location,
    private changeDetector: ChangeDetectorRef
  ) {}

  ngOnDestroy(): void {
    this.routeUpdateSubscription.unsubscribe();
    this.updateChallengesSubscription.unsubscribe();
    this.instanceUpdateSubscription.unsubscribe();
  }

  ngOnInit(): void {
    this.hideSolved = localStorage.getItem('hideSolved') === 'true';
  }

  private handleRouteUpdate(params: Params) {
    this._launchChallengeName = params['name'];
    if (this._launchChallengeName != null) {
      this.dataService.ensureChallenge(this._launchChallengeName);
    }
    this.handleChallengeUpdate();
  }

  private handleUpdateChallenges(challenges: Challenge[]) {
    this._challenges = challenges;
    this.handleChallengeUpdate();
  }

  private handleChallengeUpdate() {
    if (this._launchChallengeName != null) {
      this.showLaunchChallenge();
    }
  }

  private handleInstanceUpdate(instance: Instance | null) {
    this.instance = instance
  }

  getChallenges(category: string, includeSolved: boolean = false): Challenge[] {
    return (
      this._challenges.filter(c => c.categories.length != 0 && c.categories[0] == category)
        // hide solved
        .filter(c => includeSolved || !(this.hideSolved && (c.solvedByPlayer || c.solvedByTeam)))
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

  showLaunchChallenge() {
    let challenge = this._challenges.find(c => c.name == this._launchChallengeName);
    if (challenge) {
      this.setCurrentChallenge(challenge);
    }
  }

  openChallengeModal() {
    this.modalService.open(this.challengeModal, { size: 'lg' }).result.finally(() => {
      this.location.replaceState('/challenges');
    });
  }

  getSolves(challenge: Challenge) {
    return 0; // TODO: Fix
  }

  startChallengeInstance(challenge: string) {
    this.dataService.startInstance(challenge);
  }

  setCurrentChallenge(challenge: Challenge) {
    this.currentChallenge = challenge;
    this.changeDetector.detectChanges();
    this.flagValue = '';
    this.flagErrorMessage = '';
    this.isFlagSubmitting = false;
    this.descriptionHtml = this.domSanitizer.bypassSecurityTrustHtml(challenge.description);
    if (!this.modalService.hasOpenModals()) {
      this.openChallengeModal();
    }
    this.location.replaceState(`/challenges/${challenge.name}`);
  }

  stopChallengeInstance() {
    this.dataService.stopInstance();
  }

  submitFlag() {
    if (this.currentChallenge == null) return;
    this.flagErrorMessage = '';
    this.isFlagSubmitting = true;
    this.dataService.addSolve(this.currentChallenge.name, this.flagValue).subscribe(_ => {
      this.isFlagSubmitting = false;
    });
  }

  getCategorySolves(category: string) {
    const numChallenges = this.getChallenges(category, true).length;
    const playerSolves = this.getChallenges(category, true).filter(c => c.solvedByPlayer).length;
    return `(${playerSolves}/${numChallenges})`;
  }

  currentChallengeSolves(): Solve[] {
    const challenge = this.currentChallenge;
    if (!challenge)
      return [];
    const apiChallenge = this._challenges.find(c => c.name == challenge.name);
    if (!apiChallenge)
      return [];
    return []; // TODO: Fix
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
