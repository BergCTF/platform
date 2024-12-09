import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor, provideAuth } from 'angular-auth-oidc-client';
import { BrowserModule, bootstrapApplication } from '@angular/platform-browser';
import { AppRoutingModule } from './app/app-routing.module';
import { FormsModule } from '@angular/forms';
import { AngularDraggableModule } from 'angular2-draggable';
import { provideAnimations } from '@angular/platform-browser/animations';
import { NgxEchartsModule } from 'ngx-echarts';
import { AppComponent } from './app/app.component';
import { importProvidersFrom } from '@angular/core';

bootstrapApplication(AppComponent, {
    providers: [
        importProvidersFrom(BrowserModule, AppRoutingModule, FormsModule, AngularDraggableModule, NgxEchartsModule.forRoot({
            echarts: () => import('echarts/core'),
        })),
        provideHttpClient(withInterceptors([authInterceptor()])),
        provideAuth({
            config: {
                authority: window.location.origin,
                redirectUrl: window.location.origin + '/frontend/oidc-callback',
                postLogoutRedirectUri: window.location.origin,
                clientId: 'berg-client',
                scope: 'openid offline_access',
                responseType: 'code',
                silentRenew: true,
                useRefreshToken: true,
                renewTimeBeforeTokenExpiresInSeconds: 30,
                secureRoutes: [window.location.origin + '/api']
            },
        }),
        provideAnimations(),
    ]
}).catch(err => console.error(err));
