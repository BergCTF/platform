import { Component, OnInit, OnDestroy } from '@angular/core';
import { HelperService } from 'src/app/services/helper.service';
import { DataService } from 'src/app/services/data.service';
import { PrettyDateComponent } from '../../widgets/pretty-date/pretty-date.component';
import { ChallengeStatusComponent } from '../../widgets/challenge-status/challenge-status.component';
import { BehaviorSubject, combineLatest, map, Subscription } from 'rxjs';
import { RouterLink } from '@angular/router';
import { ChallengeDetailCategory } from 'src/app/model';
import { Instance } from 'src/app/api-model';

@Component({
    selector: 'app-challenges',
    templateUrl: './challenges.component.html',
    styleUrls: ['./challenges.component.less'],
    imports: [RouterLink, PrettyDateComponent, ChallengeStatusComponent]
})
export class ChallengesComponent implements OnInit, OnDestroy {

  public challengeDetailCategories: readonly ChallengeDetailCategory[] = [];
  public hideSolvedValue = false;
  public hasCTFStarted = true;
  public hasCTFEnded = false;
  public ctfStart: Date | null = null;
  public instance: Instance | null = null;
  public primaryChallengeCategories: readonly string[] = [];
  public challengeDifficulties: readonly string[] = [];

  private challengeDetailCategoriesSubscription: Subscription | null = null;
  private ctfStartSubscription: Subscription | null = null;
  private hasCTFStartedSubscription: Subscription | null = null;
  private hasCTFEndedSubscription: Subscription | null = null;
  private primaryChallengeCategoriesSubscription: Subscription | null = null;
  private challengeDifficultiesSubscription: Subscription | null = null;
  private instanceSubscription: Subscription | null = null;

  private filterCategory = new BehaviorSubject<string>('');
  private filterDifficulty = new BehaviorSubject<string>('');
  private hideSolved = new BehaviorSubject<boolean>(false);

  constructor(
    public dataService: DataService,
    public helper: HelperService
  ) {}

  ngOnInit(): void {
    this.hideSolvedValue = localStorage.getItem('hideSolved') === 'true';
    this.ctfStartSubscription = this.dataService.getCTFStart().subscribe(ctfStart => {
      this.ctfStart = ctfStart;
    });
    this.hasCTFStartedSubscription = this.dataService.hasCTFStarted().subscribe(hasCTFStarted => {
      this.hasCTFStarted = hasCTFStarted;
    });
    this.hasCTFEndedSubscription = this.dataService.hasCTFEnded().subscribe(hasCTFEnded => {
      this.hasCTFEnded = hasCTFEnded;
    });
    this.primaryChallengeCategoriesSubscription = this.dataService.getPrimaryChallengeCategories().subscribe(primaryChallengeCategories => {
      this.primaryChallengeCategories = primaryChallengeCategories;
    });
    this.challengeDifficultiesSubscription = this.dataService.getChallengeDifficulties().subscribe(challengeDifficulties => {
      this.challengeDifficulties = challengeDifficulties;
    });
    this.instanceSubscription = this.dataService.instance.subscribe(instance => {
      this.instance = instance;
    })
    this.challengeDetailCategoriesSubscription = combineLatest([
      this.dataService.getChallengeDetailsByCategory(),
      this.hideSolved.asObservable(),
      this.filterCategory.asObservable(),
      this.filterDifficulty.asObservable(),
      this.dataService.currentPlayerId,
    ]).pipe(map(data => {
      let [challengeDetailCategories, hideSolved, filterCategory, filterDifficulty, currentPlayerId] = data;
      let filteredChallengeDetailCategories = structuredClone(challengeDetailCategories);
      if (filterCategory != '') {
        filteredChallengeDetailCategories = filteredChallengeDetailCategories.filter(c => c.category == filterCategory);
      }
      for (let category of filteredChallengeDetailCategories) {
        if (filterDifficulty != '') {
          category.challenges = category.challenges.filter(c => c.challenge.difficulty == filterDifficulty);
        }
        if (currentPlayerId != null && hideSolved) {
          category.challenges = category.challenges.filter(c => !(c.solvedByPlayer || c.solvedByTeam));
        }
      }
      return Object.freeze(filteredChallengeDetailCategories);
    })).subscribe(challengeDetailCategories => {
      this.challengeDetailCategories = challengeDetailCategories;
    });
  }

  ngOnDestroy(): void {
    this.challengeDetailCategoriesSubscription?.unsubscribe();
    this.ctfStartSubscription?.unsubscribe();
    this.hasCTFStartedSubscription?.unsubscribe();
    this.hasCTFEndedSubscription?.unsubscribe();
    this.primaryChallengeCategoriesSubscription?.unsubscribe();
    this.challengeDifficultiesSubscription?.unsubscribe();
    this.instanceSubscription?.unsubscribe();
  }

  filterCategoryChange(value: string) {
    this.filterCategory.next(value);
  }

  filterDifficultyChange(value: string) {
    this.filterDifficulty.next(value);
  }

  hideSolvedChange(event: any) {
    let value = Boolean(event.target.checked);
    this.hideSolvedValue = value;
    this.hideSolved.next(value);
    localStorage.setItem('hideSolved', value.toString());
  }
}