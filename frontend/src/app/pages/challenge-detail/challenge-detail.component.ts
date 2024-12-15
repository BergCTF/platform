import { Component, OnDestroy, OnInit } from '@angular/core';
import { AsyncPipe, Location } from '@angular/common';
import { ActivatedRoute, Params, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { Challenge, Instance, Metadata, Solve } from 'src/app/model';
import { DataService } from 'src/app/services/data.service';
import { ChallengeStatusComponent } from 'src/app/widgets/challenge-status/challenge-status.component';
import { PrettyDateComponent } from 'src/app/widgets/pretty-date/pretty-date.component';

@Component({
  selector: 'app-challenge-detail',
  imports: [ChallengeStatusComponent, PrettyDateComponent, RouterLink, AsyncPipe],
  templateUrl: './challenge-detail.component.html',
  styleUrl: './challenge-detail.component.less'
})
export class ChallengeDetailComponent implements OnInit, OnDestroy {

  challenge = new Challenge();
  instance = new Instance();
  solves: Solve[] = [];
  isFlagSubmitting = false;
  private _metadata = new Metadata();
  private _solves: Solve[] = [];
  private _challengeName = '';
  private _challenges: Challenge[] = [];
  private routeUpdateSubscription: Subscription | undefined;
  private updateInstanceSubscription: Subscription | undefined;
  private updateMetadataSubscription: Subscription | undefined;
  private updateChallengesSubscription: Subscription | undefined;
  private updateSolvesSubscription: Subscription | undefined;

  constructor(
    public dataService: DataService,
    public location: Location,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.routeUpdateSubscription = this.route.params.subscribe(params => this.handleRouteUpdate(params));
    this.updateInstanceSubscription = this.dataService.instance.subscribe(instance => this.handleInstanceUpdate(instance));
    this.updateMetadataSubscription = this.dataService.metadata.subscribe(metadata => this.handleMetadataUpdate(metadata));
    this.updateChallengesSubscription = this.dataService.challenges.subscribe(challenges => this.handleChallengesUpdate(challenges));
    this.updateSolvesSubscription = this.dataService.solves.subscribe(solves => this.handleSolvesUpdate(solves));
  }

  ngOnDestroy(): void {
    this.routeUpdateSubscription?.unsubscribe();
    this.updateChallengesSubscription?.unsubscribe();
    this.updateMetadataSubscription?.unsubscribe();
    this.updateInstanceSubscription?.unsubscribe();
    this.updateSolvesSubscription?.unsubscribe();
  }

  submitFlag(flag: string) {
    this.isFlagSubmitting = true;
    this.dataService.addSolve(this._challengeName, flag).subscribe(_ => {
    }).add(() => {
      this.isFlagSubmitting = false;
    });
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

  private handleRouteUpdate(params: Params) {
    this._challengeName = params['name'];
    this.dataService.ensureChallenge(this._challengeName);
    this.handleChallengeUpdate();
  }

  private handleChallengesUpdate(challenges: Challenge[]) {
    this._challenges = challenges;
    this.handleChallengeUpdate();
  }

  private handleChallengeUpdate() {
    let chall = this._challenges.find(c => c.name == this._challengeName);
    if (chall != null) {
      this.challenge = chall;
    }
    this.handleSolveUpdate();
  }

  private handleInstanceUpdate(instance: Instance) {
    this.instance = instance;
  }

  private handleSolvesUpdate(solves: Solve[]) {
    this._solves = solves;
    this.handleSolveUpdate();
  }

  private handleSolveUpdate() {
    this.solves = this._solves.filter(s => s.challengeName == this._challengeName);
  }

  private handleMetadataUpdate(metadata: Metadata) {
    this._metadata = metadata;
  }
}
