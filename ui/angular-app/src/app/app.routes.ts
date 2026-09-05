import { Routes } from '@angular/router';
import { RunList } from './features/adjudication/run-list/run-list';
import { StartRun } from './features/adjudication/start-run/start-run';
import { RunDetail } from './features/adjudication/run-detail/run-detail';
import { Ingest } from './features/ingest/ingest';
import { Ask } from './features/ask/ask';
import { Ocr } from './features/ocr/ocr';
import { Login } from './features/auth/login/login';
import { Observability } from './features/observability/observability';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'runs', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'ingest', component: Ingest, canActivate: [authGuard] },
  { path: 'ask', component: Ask, canActivate: [authGuard] },
  { path: 'ocr', component: Ocr, canActivate: [authGuard] },
  { path: 'runs', component: RunList, canActivate: [authGuard] },
  { path: 'runs/new', component: StartRun, canActivate: [authGuard] },
  { path: 'runs/:id', component: RunDetail, canActivate: [authGuard] },
  { path: 'observability', component: Observability, canActivate: [authGuard] },
];
