import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { AngularDraggableModule } from 'angular2-draggable';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';

import { AppComponent } from './app.component';
import { AppRoutingModule } from './app-routing.module';
import { ChallengeStatusComponent } from './widgets/challenge-status/challenge-status.component';
import { ChallengesComponent } from './pages/challenges/challenges.component';
import { FormsModule } from '@angular/forms';
import { HomeComponent } from './pages/home/home.component';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { NotFoundComponent } from './pages/not-found/not-found.component';
import { PlayerComponent } from './pages/player/player.component';
import { ProfileSettingsComponent } from './pages/profile-settings/profile-settings.component';
import { PrettyDateComponent } from './widgets/pretty-date/pretty-date.component';

import { NgxEchartsModule } from 'ngx-echarts';
import { authInterceptor, provideAuth } from 'angular-auth-oidc-client';

@NgModule({
  declarations: [AppComponent, ChallengeStatusComponent, ChallengesComponent, HomeComponent, NotFoundComponent, PlayerComponent, ProfileSettingsComponent, PrettyDateComponent],
  imports: [
    BrowserModule,
    AppRoutingModule,
    FormsModule,
    AngularDraggableModule,
    BrowserAnimationsModule,
    NgxEchartsModule.forRoot({
      echarts: () => import('echarts/core'),
    }),
  ],
  bootstrap: [AppComponent],
  providers: [
    provideHttpClient(withInterceptors([authInterceptor()])),
    provideAuth({
      config: {
        authority: window.location.origin,
        redirectUrl: window.location.origin + "/frontend/oidc-callback",
        postLogoutRedirectUri: window.location.origin,
        clientId: 'berg-client',
        scope: 'openid offline_access',
        responseType: 'code',
        silentRenew: true,
        useRefreshToken: true,
        renewTimeBeforeTokenExpiresInSeconds: 30,
        secureRoutes: [window.location.origin]
      },
    })
  ],
})
export class AppModule {}
