import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';

import { RunDetail } from './run-detail';

describe('RunDetail', () => {
  let component: RunDetail;
  let fixture: ComponentFixture<RunDetail>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RunDetail],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'test-run-id' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RunDetail);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
