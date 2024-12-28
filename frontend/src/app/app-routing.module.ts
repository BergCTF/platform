import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ChallengesComponent } from './pages/challenges/challenges.component';
import { ProfileSettingsComponent } from './pages/profile-settings/profile-settings.component';
import { PlayerComponent } from './pages/player/player.component';
import { ChallengeDetailComponent } from './pages/challenge-detail/challenge-detail.component';
import { DynamicPageComponent } from './pages/dynamic-page/dynamic-page.component';
import { TeamDetailComponent } from './pages/team-detail/team-detail.component';
import { TeamComponent } from './pages/team/team.component';

const routes: Routes = [
  { path: 'challenges', component: ChallengesComponent },
  { path: 'challenges/:name', component: ChallengeDetailComponent },
  { path: 'profile-settings', component: ProfileSettingsComponent },
  { path: 'players/:uuid', component: PlayerComponent },
  { path: 'teams/:uuid', component: TeamDetailComponent },
  { path: 'team', component: TeamComponent },
  { path: '', pathMatch: 'full', redirectTo: '/challenges' },
  { path: '**', component: DynamicPageComponent },
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { onSameUrlNavigation: 'reload' })],
  exports: [RouterModule],
})
export class AppRoutingModule {}
