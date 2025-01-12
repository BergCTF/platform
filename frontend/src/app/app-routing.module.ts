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
  { path: 'challenges', component: ChallengesComponent, title: 'Challenges' },
  { path: 'challenges/:name', component: ChallengeDetailComponent, title: 'Challenge Detail' },
  { path: 'profile-settings', component: ProfileSettingsComponent, title: 'Profile Settings' },
  { path: 'players/:uuid', component: PlayerDetailComponent, title: 'Player Detail' },
  { path: 'player/:uuid', component: PlayerDetailComponent, title: 'Player Detail' },
  { path: 'teams/:uuid', component: TeamDetailComponent, title: 'Team Detail' },
  { path: 'team/:uuid', component: TeamDetailComponent, title: 'Team Detail' },
  { path: 'team', component: TeamComponent, title: 'Own Team' },
  { path: 'activity', component: ActivityComponent, title: 'Activity' },
  { path: 'scoreboard', component: ScoreboardComponent, title: 'Scoreboard' },
  { path: '**', component: DynamicPageComponent, title: 'Berg' },
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { onSameUrlNavigation: 'reload' })],
  exports: [RouterModule],
})
export class AppRoutingModule {}
