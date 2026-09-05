import { Routes } from '@angular/router';
import { RunList } from './features/adjudication/run-list/run-list';
import { StartRun } from './features/adjudication/start-run/start-run';
import { RunDetail } from './features/adjudication/run-detail/run-detail';
import { Ingest } from './features/ingest/ingest';
import { Ask } from './features/ask/ask';
import { Ocr } from './features/ocr/ocr';

export const routes: Routes = [
  { path: '', redirectTo: 'runs', pathMatch: 'full' },
  { path: 'ingest', component: Ingest },
  { path: 'ask', component: Ask },
  { path: 'ocr', component: Ocr },
  { path: 'runs', component: RunList },
  { path: 'runs/new', component: StartRun },
  { path: 'runs/:id', component: RunDetail },
];
