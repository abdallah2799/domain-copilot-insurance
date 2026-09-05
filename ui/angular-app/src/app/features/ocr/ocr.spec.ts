import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { Ocr } from './ocr';

describe('Ocr', () => {
  let component: Ocr;
  let fixture: ComponentFixture<Ocr>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Ocr],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Ocr);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
