import { Component, OnDestroy, OnInit } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { DataService } from "src/app/services/data.service";
import { HelperService } from "src/app/services/helper.service";
import { PrettyDateComponent } from "../../widgets/pretty-date/pretty-date.component";
import { map, Subscription } from "rxjs";
import { PlayerDetail } from "src/app/model";
import { KeyValuePipe } from "@angular/common";
import { Metadata } from "src/app/api-model";

@Component({
  selector: "app-player",
  templateUrl: "./player-detail.component.html",
  styleUrls: ["./player-detail.component.less"],
  imports: [RouterLink, PrettyDateComponent, KeyValuePipe],
})
export class PlayerDetailComponent implements OnInit, OnDestroy {
  private playerDetailSubscription: Subscription | null = null;
  private areTeamsEnabledSubscription: Subscription | null = null;
  private metadataSubscription: Subscription | null = null;

  playerDetail: PlayerDetail | null = null;
  areTeamsEnabled = false;
  metadata = new Metadata();

  constructor(
    public dataService: DataService,
    public helper: HelperService,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    let playerId = this.route.params.pipe(map((params) => params["uuid"]));
    this.playerDetailSubscription = this.dataService
      .getPlayerDetail(playerId)
      .subscribe((playerDetail) => {
        this.playerDetail = playerDetail;
      });
    this.areTeamsEnabledSubscription = this.dataService
      .areTeamsEnabled()
      .subscribe((areTeamsEnabled) => {
        this.areTeamsEnabled = areTeamsEnabled;
      });
    this.metadataSubscription = this.dataService.metadata.subscribe(
      (metadata) => {
        this.metadata = metadata;
      },
    );
  }

  ngOnDestroy(): void {
    this.playerDetailSubscription?.unsubscribe();
    this.areTeamsEnabledSubscription?.unsubscribe();
    this.metadataSubscription?.unsubscribe();
  }

  getAttributeTitle(name: string) {
    return (
      this.metadata.playerAttributes.find((a) => a.name == name)?.title ?? name
    );
  }

  getAttributeValueTitle(name: string, value: string) {
    return (
      this.metadata.playerAttributes
        .find((a) => a.name == name)
        ?.values.find((v) => v.value == value)?.title ?? value
    );
  }

  hasAttributes() {
    if (this.playerDetail) {
      if (Object.keys(this.playerDetail.attributes).length > 0) {
        return true;
      }
    }
    return false;
  }
}
