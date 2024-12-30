import { Component, OnDestroy, OnInit } from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map, Subscription } from 'rxjs';
import { Instance, ProblemDetails } from 'src/app/api-model';
import { DataService } from 'src/app/services/data.service';
import { ChallengeStatusComponent } from 'src/app/widgets/challenge-status/challenge-status.component';
import { PrettyDateComponent } from 'src/app/widgets/pretty-date/pretty-date.component';
import { HttpErrorResponse } from '@angular/common/http';
import { ChallengeDetail } from 'src/app/model';

@Component({
  selector: 'app-challenge-detail',
  imports: [ChallengeStatusComponent, PrettyDateComponent, RouterLink],
  templateUrl: './challenge-detail.component.html',
  styleUrl: './challenge-detail.component.less'
})
export class ChallengeDetailComponent implements OnInit, OnDestroy {

  challengeDetail: ChallengeDetail | null = null;
  instance: Instance | null = null;
  isFlagSubmitting = false;
  hasCTFEnded = false;
  areTeamsEnabled = false;
  flagErrorText: string | null = null;

  private challengeDetailSubscription: Subscription | null = null;
  private instanceSubscription: Subscription | null = null;
  private hasCTFEndedSubscription: Subscription | null = null;
  private areTeamsEnabledSubscription: Subscription | null = null;

  constructor(
    public dataService: DataService,
    public location: Location,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    let name = this.route.params.pipe(map(params => params['name']));
    this.challengeDetailSubscription = this.dataService.getChallengeDetail(name).subscribe(challengeDetail => {
      this.challengeDetail = challengeDetail;
    });
    this.instanceSubscription = this.dataService.instance.subscribe(instance => {
      this.instance = instance;
    });
    this.hasCTFEndedSubscription = this.dataService.hasCTFEnded().subscribe(hasCTFEnded => {
      this.hasCTFEnded = hasCTFEnded;
    });
    this.areTeamsEnabledSubscription = this.dataService.areTeamsEnabled().subscribe(areTeamsEnabled => {
      this.areTeamsEnabled = areTeamsEnabled;
    });
  }

  ngOnDestroy(): void {
    this.challengeDetailSubscription?.unsubscribe();
    this.instanceSubscription?.unsubscribe();
    this.hasCTFEndedSubscription?.unsubscribe();
    this.areTeamsEnabledSubscription?.unsubscribe();
  }

  submitFlag(challenge: string, flag: string) {
    this.isFlagSubmitting = true;
    this.dataService.addSolve(challenge, flag).subscribe({
      next: _ => {
        this.flagErrorText = null;
      },
      error: err => {
        let httpError = err as HttpErrorResponse;
        let problemDetails = httpError.error as ProblemDetails;
        this.flagErrorText = problemDetails.detail;
      }
    }).add(() => {
      this.isFlagSubmitting = false;
    });
  }
}
