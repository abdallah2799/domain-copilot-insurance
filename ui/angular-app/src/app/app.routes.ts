import { Routes } from '@angular/router';
import { RunList } from './features/adjudication/run-list/run-list';
import { StartRun } from './features/adjudication/start-run/start-run';
import { RunDetail } from './features/adjudication/run-detail/run-detail';

export const routes: Routes = [
  { path: '', redirectTo: 'runs', pathMatch: 'full' },
  { path: 'runs', component: RunList },
  { path: 'runs/new', component: StartRun },
  { path: 'runs/:id', component: RunDetail },
];
