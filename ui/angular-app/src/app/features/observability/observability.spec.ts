import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { Observability } from './observability';

describe('Observability', () => {
  let component: Observability;
  let fixture: ComponentFixture<Observability>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Observability],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Observability);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
