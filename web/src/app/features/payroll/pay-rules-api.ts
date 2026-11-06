import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CompanyHoliday,
  HolidayCalendar,
  HolidayType,
  PayRules,
  PayRulesFactors,
} from './payroll.models';

/** The pay rules and holiday calendar endpoints, one method each. */
@Injectable({ providedIn: 'root' })
export class PayRulesApi {
  private readonly http = inject(HttpClient);

  rules(): Observable<PayRules> {
    return this.http.get<PayRules>('/api/payroll-rules');
  }

  saveRules(factors: PayRulesFactors, version: number): Observable<PayRules> {
    return this.http.put<PayRules>('/api/payroll-rules', { ...factors, version });
  }

  holidays(year: number): Observable<HolidayCalendar> {
    return this.http.get<HolidayCalendar>('/api/payroll-holidays', {
      params: new HttpParams().set('year', year),
    });
  }

  addHoliday(date: string, name: string, type: HolidayType): Observable<CompanyHoliday> {
    return this.http.post<CompanyHoliday>('/api/payroll-holidays', { date, name, type });
  }

  changeHoliday(
    id: string,
    name: string,
    type: HolidayType,
    version: number,
  ): Observable<CompanyHoliday> {
    return this.http.put<CompanyHoliday>(`/api/payroll-holidays/${id}`, { name, type, version });
  }

  removeHoliday(id: string, version: number): Observable<void> {
    return this.http.post<void>(`/api/payroll-holidays/${id}/remove`, { version });
  }
}
