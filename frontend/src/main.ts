import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor, provideAuth } from 'angular-auth-oidc-client';
import { BrowserModule, bootstrapApplication } from '@angular/platform-browser';
import { AppRoutingModule } from './app/app-routing.module';
import { FormsModule } from '@angular/forms';
import { AngularDraggableModule } from 'angular2-draggable';
import { provideAnimations } from '@angular/platform-browser/animations';
import { NgxEchartsModule } from 'ngx-echarts';
import { AppComponent } from './app/app.component';
import { importProvidersFrom, inject, provideAppInitializer } from '@angular/core';
import { DataService } from './app/services/data.service';
import { EMPTY, mergeMap, take } from 'rxjs';

bootstrapApplication(AppComponent, {
    providers: [
        importProvidersFrom(BrowserModule, AppRoutingModule, FormsModule, AngularDraggableModule, NgxEchartsModule.forRoot({
            echarts: () => import('echarts/core'),
        })),
        provideHttpClient(withInterceptors([authInterceptor()])),
        provideAuth({
            config: {
                configId: 'berg',
                authority: window.location.origin,
                redirectUrl: window.location.origin + '/frontend/oidc-callback',
                postLogoutRedirectUri: window.location.origin,
                clientId: 'berg-client',
                scope: 'openid offline_access',
                responseType: 'code',
                silentRenew: true,
                useRefreshToken: true,
                ignoreNonceAfterRefresh: true,
                renewTimeBeforeTokenExpiresInSeconds: 30,
                secureRoutes: [window.location.origin + '/api']
            },
        }),
        provideAnimations(),
        provideAppInitializer(() => {
            let dataService = inject(DataService);
            return dataService.refreshMetadata().pipe(mergeMap(metadata => {
                if (metadata.allowAnonymousAccess) {
                    return dataService.refreshPages();
                } else {
                    dataService.login();
                    return EMPTY;
                }
            }), mergeMap(_ => dataService.loginEvents.pipe(take(1))));
        }),
    ]
}).catch(err => console.error(err));
