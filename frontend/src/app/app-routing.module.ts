import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ChallengesComponent } from './pages/challenges/challenges.component';
import { ProfileSettingsComponent } from './pages/profile-settings/profile-settings.component';
import { PlayerDetailComponent } from './pages/player-detail/player-detail.component';
import { ChallengeDetailComponent } from './pages/challenge-detail/challenge-detail.component';
import { DynamicPageComponent } from './pages/dynamic-page/dynamic-page.component';
import { TeamDetailComponent } from './pages/team-detail/team-detail.component';
import { TeamComponent } from './pages/team/team.component';
import { ScoreboardComponent } from './pages/scoreboard/scoreboard.component';
import { ActivityComponent } from './pages/activity/activity.component';

const routes: Routes = [
  { path: 'challenges', component: ChallengesComponent },
  { path: 'challenges/:name', component: ChallengeDetailComponent },
  { path: 'profile-settings', component: ProfileSettingsComponent },
  { path: 'players/:uuid', component: PlayerDetailComponent },
  { path: 'player/:uuid', component: PlayerDetailComponent },
  { path: 'teams/:uuid', component: TeamDetailComponent },
  { path: 'team/:uuid', component: TeamDetailComponent },
  { path: 'team', component: TeamComponent },
  { path: 'activity', component: ActivityComponent },
  { path: 'scoreboard', component: ScoreboardComponent },
  { path: '**', component: DynamicPageComponent },
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { onSameUrlNavigation: 'reload' })],
  exports: [RouterModule],
})
export class AppRoutingModule {}
