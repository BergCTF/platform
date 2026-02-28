/// <reference types="@angular/localize" />

import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor, provideAuth } from 'angular-auth-oidc-client';
import { BrowserModule, bootstrapApplication } from '@angular/platform-browser';
import { AppRoutingModule } from './app/app-routing.module';
import { FormsModule } from '@angular/forms';
import { AngularDraggableModule } from 'angular2-draggable';
import { provideEchartsCore } from 'ngx-echarts';
import { AppComponent } from './app/app.component';
import { importProvidersFrom, inject, provideAppInitializer } from '@angular/core';
import { DataService } from './app/services/data.service';
import { catchError, mergeMap, NEVER, take } from 'rxjs';
import * as echarts from 'echarts/core';
import { BarChart, LineChart } from 'echarts/charts';
import { GridComponent, LegendComponent, TitleComponent, TooltipComponent } from 'echarts/components';
import { CanvasRenderer, SVGRenderer } from 'echarts/renderers';

echarts.use([
    BarChart, LineChart,
    GridComponent, TitleComponent, TooltipComponent, LegendComponent,
    CanvasRenderer, SVGRenderer]);

bootstrapApplication(AppComponent, {
    providers: [
        importProvidersFrom(BrowserModule, AppRoutingModule, FormsModule, AngularDraggableModule),
        provideEchartsCore({ echarts }),
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
        provideAppInitializer(() => {
            let dataService = inject(DataService);
            return dataService.loginEvents.pipe(take(1), mergeMap(loginResponse => {
                return dataService.refreshMetadata().pipe(
                    catchError((err, _) => {
                        let httpError = err as HttpErrorResponse;
                        if (httpError.status == 401) {
                            console.log("Redirecting to authorization endpoint since metadata request got error status 401.");
                            dataService.login();
                        } else {
                            console.error("Failed to fetch metadata.");
                            console.error(err);
                        }
                        return NEVER; // Stall execution since login() triggers a redrect that forces a page reload anyways.
                    }),
                    mergeMap(metadata => {
                        if (!metadata.allowAnonymousAccess && !loginResponse.isAuthenticated) {
                            console.log("Redirecting to authorization endpoint since anonymous access is disabled and the player is not logged in.");
                            dataService.login();
                            return NEVER; // Stall execution since login() triggers a redrect that forces a page reload anyways.
                        } else {
                            dataService.refreshAllData();
                            if (loginResponse.isAuthenticated){
                                dataService.refreshWebSocket(loginResponse.accessToken);
                            } else {
                                dataService.refreshWebSocket(null);
                            }
                            return dataService.refreshPages();
                        }
                    })
                );
            }));
        }),
    ]
}).catch(err => console.error(err));
