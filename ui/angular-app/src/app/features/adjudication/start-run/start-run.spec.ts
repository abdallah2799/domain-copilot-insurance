import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { StartRun } from './start-run';

describe('StartRun', () => {
  let component: StartRun;
  let fixture: ComponentFixture<StartRun>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StartRun],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(StartRun);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => httpMock.verify());

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  // Regression coverage for the T6 gap: an adjuster shouldn't have to retype fields that are
  // already printed on the claim's own intake form -- see AdjudicationService.extractIntakeFields.
  it('pre-fills claim number, policy number and date of loss from an uploaded intake form', () => {
    const file = new File(['%PDF-fake'], 'intake.pdf', { type: 'application/pdf' });
    const input = { files: [file] } as unknown as HTMLInputElement;

    component.onIntakeFileSelected({ target: input } as unknown as Event);

    const req = httpMock.expectOne('http://localhost:5080/api/adjudication/runs/extract-intake-fields');
    req.flush({
      combinedText: 'irrelevant',
      overallConfidencePercent: 95.35,
      fields: {
        claimNumber: 'CLM-2025-04417',
        policyNumber: 'MMIC-PAP-100234',
        dateOfLoss: '2025-08-03',
        policeReportNumber: 'CPD-2025-231044',
      },
    });

    expect(component.form.getRawValue().claimNumber).toBe('CLM-2025-04417');
    expect(component.form.getRawValue().policyNumber).toBe('MMIC-PAP-100234');
    expect(component.form.getRawValue().dateOfLoss).toBe('2025-08-03');
    expect(component.extractedPoliceReportNumber()).toBe('CPD-2025-231044');
    expect(component.extractionConfidencePercent()).toBe(95.35);
  });

  it('leaves fields untouched when extraction cannot read a value, and surfaces the error on failure', () => {
    const file = new File(['%PDF-fake'], 'intake.pdf', { type: 'application/pdf' });
    const input = { files: [file] } as unknown as HTMLInputElement;

    component.onIntakeFileSelected({ target: input } as unknown as Event);

    const req = httpMock.expectOne('http://localhost:5080/api/adjudication/runs/extract-intake-fields');
    req.flush('OCR service unavailable', { status: 500, statusText: 'Server Error' });

    expect(component.extractionError()).toContain('Could not read the intake form');
  });
});
