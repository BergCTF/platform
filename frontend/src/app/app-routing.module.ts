import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ChallengesComponent } from './pages/challenges/challenges.component';
import { NotFoundComponent } from './pages/not-found/not-found.component';
import { HomeComponent } from './pages/home/home.component';
import { ProfileSettingsComponent } from './pages/profile-settings/profile-settings.component';
import { PlayerComponent } from './pages/player/player.component';
import { ChallengeDetailComponent } from './pages/challenge-detail/challenge-detail.component';

const routes: Routes = [
  { path: 'challenges', component: ChallengesComponent },
  { path: 'challenges/:name', component: ChallengeDetailComponent },
  { path: 'home', component: HomeComponent },
  { path: 'profile-settings', component: ProfileSettingsComponent },
  { path: 'players/:uuid', component: PlayerComponent },
  { path: '', component: HomeComponent },
  { path: '**', component: NotFoundComponent },
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { onSameUrlNavigation: 'reload' })],
  exports: [RouterModule],
})
export class AppRoutingModule {}
