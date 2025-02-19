import { HttpErrorResponse } from "@angular/common/http";
import { Component, TemplateRef } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { NgbModal } from "@ng-bootstrap/ng-bootstrap";
import { Subscription } from "rxjs";
import { ProblemDetails } from "src/app/api-model";
import { TeamDetail } from "src/app/model";
import { DataService } from "src/app/services/data.service";

@Component({
  selector: "app-team",
  templateUrl: "./team.component.html",
  styleUrl: "./team.component.less",
  imports: [RouterLink, FormsModule],
})
export class TeamComponent {
  private teamDetailsSubscription: Subscription | null = null;
  private currentTeamJoinTokenSubscription: Subscription | null = null;
  private areTeamsEnabledSubscription: Subscription | null = null;

  joinError: string | null = null;
  createError: string | null = null;
  teamJoinToken: string | null = null;
  joinToken: string = "";
  teamName: string = "";
  teamDetail: TeamDetail | null = null;
  areTeamsEnabled = false;

  constructor(
    public dataService: DataService,
    private modalService: NgbModal,
  ) {}

  ngOnInit(): void {
    this.teamDetailsSubscription = this.dataService
      .getCurrentTeamDetail()
      .subscribe((teamDetail) => {
        this.teamDetail = teamDetail;
      });
    this.areTeamsEnabledSubscription = this.dataService
      .areTeamsEnabled()
      .subscribe((areTeamsEnabled) => {
        this.areTeamsEnabled = areTeamsEnabled;
      });

    this.currentTeamJoinTokenSubscription =
      this.dataService.currentTeamJoinToken.subscribe((token) => {
        this.teamJoinToken = token;
      });
  }

  ngOnDestroy(): void {
    this.teamDetailsSubscription?.unsubscribe();
    this.currentTeamJoinTokenSubscription?.unsubscribe();
    this.areTeamsEnabledSubscription?.unsubscribe();
  }

  open(content: TemplateRef<any>) {
    this.modalService.open(content);
  }

  leaveTeam() {
    this.dataService.leaveTeam().subscribe(() => {});
  }

  joinTeam() {
    this.dataService.joinTeam(this.joinToken).subscribe({
      next: () => {
        this.joinToken = "";
        this.joinError = null;
      },
      error: (err) => {
        let httpError = err as HttpErrorResponse;
        let problemDetails = httpError.error as ProblemDetails;
        this.joinError = problemDetails.detail;
      },
    });
  }

  createTeam() {
    this.dataService.createTeam(this.teamName).subscribe({
      next: () => {
        this.teamName = "";
        this.createError = null;
      },
      error: (err) => {
        let httpError = err as HttpErrorResponse;
        let problemDetails = httpError.error as ProblemDetails;
        this.createError = problemDetails.detail;
      },
    });
  }
}
