import { inject, NgModule } from '@angular/core';
import { ActivatedRouteSnapshot, ResolveFn, RouterModule, Routes } from '@angular/router';
import { ChallengesComponent } from './pages/challenges/challenges.component';
import { ProfileSettingsComponent } from './pages/profile-settings/profile-settings.component';
import { PlayerDetailComponent } from './pages/player-detail/player-detail.component';
import { ChallengeDetailComponent } from './pages/challenge-detail/challenge-detail.component';
import { DynamicPageComponent } from './pages/dynamic-page/dynamic-page.component';
import { TeamDetailComponent } from './pages/team-detail/team-detail.component';
import { TeamComponent } from './pages/team/team.component';
import { ScoreboardComponent } from './pages/scoreboard/scoreboard.component';
import { ActivityComponent } from './pages/activity/activity.component';
import { combineLatest, map, Observable, of, take } from 'rxjs';
import { DataService } from './services/data.service';

const challengeTitleResolver: ResolveFn<string> = (
  route: ActivatedRouteSnapshot
): Observable<string> => {
  const name = route.paramMap.get('name');
  const dataService = inject(DataService);
  return combineLatest([dataService.metadata, dataService.getChallengeDetail(of(name))]).pipe(take(1), map(params => {
    const [metadata, challenge] = params;
    if (challenge) {
      return `${metadata.eventName} - Challenge: ${challenge.challenge.name}`;
    }
    return `${metadata.eventName} - Challenge: Not Found`;
  })
  );
};

const playerTitleResolver: ResolveFn<string> = (
  route: ActivatedRouteSnapshot
): Observable<string> => {
  const id = route.paramMap.get('uuid');
  const dataService = inject(DataService);
  return combineLatest([dataService.metadata, dataService.getPlayerDetail(of(id))]).pipe(take(1), map(params => {
    const [metadata, player] = params;
    if (player) {
      return `${metadata.eventName} - Player: ${player.name}`;
    }
    return `${metadata.eventName} - Player: Not Found`;
  }));
};

const teamTitleResolver: ResolveFn<string> = (
  route: ActivatedRouteSnapshot
): Observable<string> => {
  const id = route.paramMap.get('uuid');
  const dataService = inject(DataService);
  return combineLatest([dataService.metadata, dataService.getTeamDetail(of(id))]).pipe(take(1), map(params => {
      const [metadata, team] = params;
      if (team) {
        return `${metadata.eventName} - Team: ${team.name}`;
      }
      return `${metadata.eventName} - Team: Not Found`;
    })
  );
};

const dynamicPageTitleResolver: ResolveFn<string> = (
  route: ActivatedRouteSnapshot
): Observable<string> => {
  const url = route.pathFromRoot
    .map(v => v.url.map(segment => segment.toString()).join('/'))
    .join('/');
  const dataService = inject(DataService);
  return combineLatest([dataService.metadata, dataService.pages]).pipe(take(1), map(params => {
    const [metadata, pages] = params;

    let page = pages.map(p => {
      if (!p.path.startsWith('/'))
        p.path = '/' + p.path;
      return p;
    }).find(p => p.path == url) || null;
    if (page) {
      return `${metadata.eventName}: ${page.title}`;
    }
    return `${metadata.eventName}: Not Found`;
  }));
};

function prefixedTitleResolver(title: string): ResolveFn<string> {
  return (
    route: ActivatedRouteSnapshot
  ): Observable<string> => {
    const dataService = inject(DataService);
    return dataService.metadata.pipe(take(1), map(metadata => {
      return `${metadata.eventName}: ${title}`;
    }));
  }
}

const routes: Routes = [
  { path: 'challenges', component: ChallengesComponent, title: prefixedTitleResolver('Challenges') },
  { path: 'challenges/:name', component: ChallengeDetailComponent, title: challengeTitleResolver },
  { path: 'profile-settings', component: ProfileSettingsComponent, title: prefixedTitleResolver('Profile Settings') },
  { path: 'players/:uuid', component: PlayerDetailComponent, title: playerTitleResolver },
  { path: 'player/:uuid', component: PlayerDetailComponent, title: playerTitleResolver },
  { path: 'teams/:uuid', component: TeamDetailComponent, title: teamTitleResolver },
  { path: 'team/:uuid', component: TeamDetailComponent, title: teamTitleResolver },
  { path: 'team', component: TeamComponent, title: prefixedTitleResolver('Team Settings') },
  { path: 'activity', component: ActivityComponent, title: prefixedTitleResolver('Activity') },
  { path: 'scoreboard', component: ScoreboardComponent, title: prefixedTitleResolver('Scoreboard') },
  { path: '**', component: DynamicPageComponent, title: dynamicPageTitleResolver },
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { onSameUrlNavigation: 'reload' })],
  exports: [RouterModule]
})
export class AppRoutingModule {}
