import { Component, OnInit, ElementRef, ViewChild, inject, ChangeDetectorRef } from '@angular/core';
import { Location } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Challenge, CtfChallenges, PlayerSelf, SubmitFlagResult, PlayerSolve } from 'src/app/model';
import { ApiService } from 'src/app/services/api.service';
import { ActivatedRoute, Router } from '@angular/router';
import { Helpers } from 'src/app/services/helpers.service';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
  selector: 'app-challenges',
  templateUrl: './challenges.component.html',
  styleUrls: ['./challenges.component.less'],
})
export class ChallengesComponent implements OnInit {
  @ViewChild('challengeModal') challengeModal!: ElementRef;
  private modalService = inject(NgbModal);
  public ctfChallenges: CtfChallenges = new CtfChallenges();
  public playerSelf: PlayerSelf = new PlayerSelf();
  public currentChallenge: Challenge | null = null;
  public descriptionHtml: SafeHtml = '';
  public flagValue = '';
  public flagErrorMessage = '';
  public isFlagSubmitting = false;
  private launchChallenge = '';
  public hideSolved = false;
  public filterCategory = '';
  public filterDifficulty = '';

  constructor(
    public apiService: ApiService,
    private domSanitizer: DomSanitizer,
    private router: Router,
    private route: ActivatedRoute,
    public helpers: Helpers,
    private location: Location,
    private changeDetector: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.ctfChallenges = this.apiService.getCtfChallenges();
    this.playerSelf = this.apiService.getPlayerSelf();
    this.hideSolved = localStorage.getItem('hideSolved') === 'true';
    this.route.params.subscribe(params => {
      this.launchChallenge = params['name'];
      if (this.launchChallenge != null) {
        this.apiService.refreshCtfChallenges(() => this.showLaunchChallenge());
      }
    });
  }

  getChallenges(category: string, includeSolved: boolean = false): Challenge[] {
    return (
      this.ctfChallenges.challengesByCategory[category]
        // hide solved
        .filter(c => includeSolved || !(this.hideSolved && (c.solvedByPlayer || c.solvedByTeam)))
        // filter by difficulty
        .filter(c => this.filterDifficulty === '' || this.filterDifficulty === c.difficulty)
        // filter by category
        .filter(c => this.filterCategory === '' || c.categories.includes(this.filterCategory))
    );
  }

  getStart(): Date {
    return new Date(this.ctfChallenges?.start);
  }

  getEnd(): Date {
    return new Date(this.ctfChallenges?.end);
  }

  getServerTime(): Date {
    return new Date(this.ctfChallenges?.serverTime);
  }

  showLaunchChallenge() {
    this.ctfChallenges = this.apiService.getCtfChallenges();
    const allChallenges = Object.values(this.ctfChallenges.challengesByCategory).reduce((acc, val) => acc.concat(val), []);
    const challenge = allChallenges.find(c => c.name == this.launchChallenge);
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
    return challenge.playerSolves.length || challenge.teamSolves.length;
  }

  startChallengeInstance(challenge: string) {
    this.apiService.startChallengeInstance(challenge).subscribe(() => {
      console.log('Challenge instance started: ' + challenge);
    });
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
    this.apiService.stopChallengeInstance().subscribe(() => {
      console.log('Challenge instance stopped');
      this.playerSelf = this.apiService.getPlayerSelf();
    });
  }

  submitFlag() {
    if (this.currentChallenge == null) return;
    this.flagErrorMessage = '';
    this.isFlagSubmitting = true;
    this.apiService.submitFlag(this.currentChallenge.name, this.flagValue).subscribe(
      res => {
        this.isFlagSubmitting = false;
        this.ctfChallenges = this.apiService.getCtfChallenges();
        const result = res as SubmitFlagResult;
        if (result == SubmitFlagResult.Correct) {
          if (this.currentChallenge == null) return;
          this.currentChallenge.solvedByPlayer = true;
          this.currentChallenge.solvedByTeam = true;
          this.apiService.refreshCtfChallenges();
          this.apiService.refreshPlayerScoreboard();
        } else if (result == SubmitFlagResult.Incorrect) {
          this.flagErrorMessage = 'The flag you have provided is incorrect.';
        } else if (result == SubmitFlagResult.AlreadySolved) {
          if (this.currentChallenge == null) return;
        } else if (result == SubmitFlagResult.RateLimited) {
          this.flagErrorMessage = 'You are being rate limited.';
        } else if (result == SubmitFlagResult.CtfNotStarted) {
          this.flagErrorMessage = 'CTF has not started yet.';
        } else if (result == SubmitFlagResult.CtfHasEnded) {
          this.flagErrorMessage = 'CTF has ended.';
        }
      },
      () => {
        this.isFlagSubmitting = false;
      }
    );
  }

  getCategorySolves(category: string) {
    const numChallenges = this.getChallenges(category, true).length;
    const playerSolves = this.getChallenges(category, true).filter(c => c.solvedByPlayer).length;
    return `(${playerSolves}/${numChallenges})`;
  }

  currentChallengeSolves(): PlayerSolve[] {
    const challenge = this.currentChallenge;
    if (!challenge) return [];
    const allChallenges = this.helpers.getAllChallenges();
    const apiChallenge = allChallenges.find(c => c.name == challenge.name);
    if (!apiChallenge) return [];
    return apiChallenge.playerSolves || apiChallenge.teamSolves;
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

  aprilFools() {
    return new Date().getMonth() == 3 && new Date().getDate() == 1;
  }
}
