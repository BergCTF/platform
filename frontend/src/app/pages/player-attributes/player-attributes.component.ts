import { Component, OnDestroy, OnInit } from "@angular/core";
import { RouterLink } from "@angular/router";
import { NgbTooltip } from "@ng-bootstrap/ng-bootstrap";
import { combineLatest, Subscription } from "rxjs";
import { PlayerAttribute } from "src/app/api-model";
import { DataService } from "src/app/services/data.service";

@Component({
  selector: "app-player-attributes",
  templateUrl: "./player-attributes.component.html",
  styleUrl: "./player-attributes.component.less",
  imports: [NgbTooltip, RouterLink],
})
export class PlayerAttributesComponent implements OnInit, OnDestroy {
  public hasMissingRequiredAttributes: boolean = true;
  public currentPlayerAttributes: CurrentPlayerAttribute[] = [];
  private attributeSubscription: Subscription | undefined;

  constructor(private dataService: DataService) {}

  ngOnInit(): void {
    this.attributeSubscription = combineLatest([
      this.dataService.metadata,
      this.dataService.currentPlayer,
    ]).subscribe((params) => {
      const [metadata, currentPlayer] = params;
      if (currentPlayer == null) {
        return;
      }
      let requiredAttributes = metadata.playerAttributes;
      let playerAttributes = new Set(Object.keys(currentPlayer.attributes));

      let currentPlayerAttributes: CurrentPlayerAttribute[] = [];
      let hasMissingRequiredAttributes = false;
      requiredAttributes.forEach((a) => {
        let attr = new CurrentPlayerAttribute();
        attr.attr = a;
        if (playerAttributes.has(a.name)) {
          attr.selectedValue = currentPlayer.attributes[a.name];
        } else {
          attr.selectedValue = null;
          if (a.required) {
            hasMissingRequiredAttributes = true;
          }
        }
        currentPlayerAttributes.push(attr);
      });

      this.currentPlayerAttributes = currentPlayerAttributes;
      this.hasMissingRequiredAttributes = hasMissingRequiredAttributes;
    });
  }

  ngOnDestroy(): void {
    this.attributeSubscription?.unsubscribe();
  }

  setPlayerAttribute(name: string, value: string) {
    let attrs: Record<string, string> = {};
    attrs[name] = value;
    this.dataService.setPlayerAttributes(attrs).subscribe((_) => {
      this.dataService.refreshCurrentPlayer().subscribe();
    });
  }
}

class CurrentPlayerAttribute {
  public attr: PlayerAttribute = new PlayerAttribute();
  public selectedValue: string | null = null;
}
