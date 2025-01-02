import { SlicePipe } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgbPaginationModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { EChartsCoreOption } from 'echarts/core';
import { SeriesOption } from 'echarts/types/dist/shared';
import { NgxEchartsDirective } from 'ngx-echarts';
import { BehaviorSubject, combineLatest, distinctUntilChanged, map, Subscription } from 'rxjs';
import { Metadata } from 'src/app/api-model';
import { ChallengeDetail, PlayerDetail, ScoreboardChallengeEntry, ScoreboardRanking, TeamDetail } from 'src/app/model';
import { DataService } from 'src/app/services/data.service';
import { HelperService } from 'src/app/services/helper.service';
import { PrettyDateComponent } from 'src/app/widgets/pretty-date/pretty-date.component';

@Component({
  selector: 'app-scoreboard',
  templateUrl: './scoreboard.component.html',
  styleUrl: './scoreboard.component.less',
  imports: [RouterLink, PrettyDateComponent, NgxEchartsDirective, SlicePipe, NgbTooltipModule, NgbPaginationModule],
})
export class ScoreboardComponent implements OnInit, OnDestroy {

  public echartsOptions: EChartsCoreOption = {};

  public scoreboardRanking: readonly ScoreboardRanking[] = [];
  public areTeamsEnabled = false;
  public primaryChallengeCategories: readonly string[] = [];
  public advancedView = false;
  public hoveredChallenge = '';
  public pageIndex = 1;
  public pageSize = 30;

  private scoreboardSubscription: Subscription | null = null;
  private searchTextSubscription: Subscription | null = null;
  private primaryChallengeCategoriesSubscription: Subscription | null = null;
  private areTeamsEnabledSubsciption: Subscription | null = null;
  private chartSubscription: Subscription | null = null;
  private searchText = new BehaviorSubject<string>('');

  constructor(
    public helper: HelperService,
    private dataService: DataService
  ) {}

  ngOnInit(): void {
    let searchTextObservable = this.searchText.asObservable().pipe(distinctUntilChanged());
    this.scoreboardSubscription = combineLatest([
      this.dataService.getScoreboard(),
      searchTextObservable
    ]).pipe(
      map(params => {
        const [scoreboard, searchText] = params;
        var searchTextLower = searchText.toLowerCase();
        return scoreboard.filter(s => s.name.toLowerCase().includes(searchTextLower));
      })
    ).subscribe(scoreboardRanking => {
      this.scoreboardRanking = scoreboardRanking;
    });
    this.primaryChallengeCategoriesSubscription = this.dataService.getPrimaryChallengeCategories().subscribe(primaryChallengeCategories => {
      this.primaryChallengeCategories = primaryChallengeCategories;
    });
    this.areTeamsEnabledSubsciption = this.dataService.areTeamsEnabled().subscribe(areTeamsEnabled => {
      this.areTeamsEnabled = areTeamsEnabled;
    });
    this.chartSubscription = combineLatest([
      this.dataService.metadata,
      this.dataService.getChallengeDetails(),
      this.dataService.getPlayerDetails(),
      this.dataService.getTeamDetails()
    ]).subscribe(params => {
      const [metadata, challenges, players, teams] = params;
      this.updateChart(metadata, challenges, players, teams)
    });

    this.searchTextSubscription = searchTextObservable.subscribe(text => {
      // Reset the page index to page 1 as soon as the search text changes.
      this.pageIndex = 1;
    });
  }

  ngOnDestroy(): void {
    this.scoreboardSubscription?.unsubscribe();
    this.searchTextSubscription?.unsubscribe();
    this.primaryChallengeCategoriesSubscription?.unsubscribe();
    this.areTeamsEnabledSubsciption?.unsubscribe();
    this.chartSubscription?.unsubscribe();
  }

  toggleAdvancedView() {
    this.advancedView = !this.advancedView;
  }

  setHover(hoveredChallenge: string) {
    this.hoveredChallenge = hoveredChallenge;
  }

  searchTextChanged(searchText: string) {
    this.searchText.next(searchText);
  }

  pageSizeChanged(pageSizeValue: string) {
    this.pageSize = parseInt(pageSizeValue);
  }

  getChallengeClasses(entry: ScoreboardChallengeEntry) {
    let classes = 'bi me-1';
    if (this.hoveredChallenge !== '' && this.hoveredChallenge !== entry.challenge.name) {
      classes += ' other-hover ';
    }
    if (entry.solved) {
      return classes + ' bi-hexagon-fill ' + this.helper.getDifficultyTextColorClass(entry.challenge.difficulty);
    } else {
      return classes + ' bi-hexagon';
    }
  }

  updateChart(metadata: Metadata, challenges: readonly ChallengeDetail[], players: readonly PlayerDetail[], teams: readonly TeamDetail[]) {
    const challengePoints: { [key: string]: number } = {};
    challenges.forEach(c => {
      challengePoints[c.challenge.name] = c.value;
    });

    const scores: { [key: string]: { name: string; scores: [string, number][]; totalPoints: number } } = {};

    if (metadata.teams) {
      let teamsCopy = structuredClone(teams).filter(_ => true);
      teamsCopy.sort((a,b) => b.score - a.score);
      teamsCopy = teamsCopy.slice(0, 10);
      let solves = teamsCopy.flatMap(p => p.solves);
      solves.sort((a, b) => a.solvedAt.getTime() - b.solvedAt.getTime())
      teamsCopy.forEach(team => {
        scores[team.id] = { name: team.name, scores: [], totalPoints: 0 };
      });
      solves.forEach(solve => {
        if(solve.teamId) {
          if (scores[solve.teamId]) {
            const points = challengePoints[solve.challengeName] || 0;
            scores[solve.teamId].totalPoints += points;
            scores[solve.teamId].scores.push([solve.solvedAt.toISOString(), scores[solve.teamId].totalPoints]);
          }
        }
      });
    } else {
      let playersCopy = structuredClone(players).filter(_ => true);
      playersCopy.sort((a,b) => b.score - a.score);
      playersCopy = playersCopy.slice(0, 10);
      let solves = playersCopy.flatMap(p => p.solves);
      solves.sort((a, b) => a.solvedAt.getTime() - b.solvedAt.getTime())
      playersCopy.forEach(player => {
        scores[player.id] = { name: player.name, scores: [], totalPoints: 0 };
      });
      solves.forEach(solve => {
        if (scores[solve.playerId]) {
          const points = challengePoints[solve.challengeName] || 0;
          scores[solve.playerId].totalPoints += points;
          scores[solve.playerId].scores.push([solve.solvedAt.toISOString(), scores[solve.playerId].totalPoints]);
        }
      });
    }

    const echartData: SeriesOption[] = [];
    for (const score in scores) {
      echartData.push({
        type: 'line',
        symbol: 'none',
        lineStyle: {
          width: 1.6,
        },
        name: scores[score].name,
        data: scores[score].scores,
      });
    }

    this.echartsOptions = {
      tooltip: {
        trigger: 'axis',
        axisPointer: {
          type: 'cross',
        },
      },
      legend: {
        type: 'scroll',
        orient: 'horizontal',
        align: 'left',
        bottom: 0,
        textStyle: {
          color: '#6E7079',
        },
      },
      toolbox: {
        feature: {
          saveAsImage: {},
          dataZoom: {},
        },
      },
      grid: {
        containLabel: true,
      },
      xAxis: {
        type: 'time',
      },
      yAxis: {},
      series: echartData,
    };
  }
}
