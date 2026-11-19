export interface Salary {
  grossCashSalary: number;
  taxableIncome: number;
  incomeTax: number;
  nationalInsurance: number;
  healthInsurance: number;
  employeePension: number;
  studyFund: number;
  employeeNetParticipation: number;
  electricReimbursementGross: number;
  netSalary: number;
  displayNetSalary: number;
}
export interface Eligibility {
  eligible: boolean;
  reason: string;
  budget: number;
  familyEmployeeShare: number;
  familyEmployerShare: number;
}
export interface Employee {
  id: string;
  name: string;
  occupation: string;
  group: string;
  groupBudget: number;
  baseSalary: number;
  commission: number;
  bonus: number;
  pensionRate: number;
  studyFundRate: number;
  creditPoints: number;
  eligibleChildren: number;
  currentCarId: string | null;
  currentCar: string;
  waiver: Eligibility;
  maintenanceAllowance: number;
  currentSalary: Salary;
}
export interface Vehicle {
  id: string;
  name: string;
  manufacturer: string;
  drive: string;
  group: string;
  monthlyCost: number;
  participation: number;
  valueOfUse: number;
  key: {
    taxYear: number;
    registrationYear: number;
    vehicleTypeCode: string;
    manufacturerCode: string;
    modelCode: string;
  };
  eligibility: Eligibility;
}
export interface Context {
  payrollTaxYear: number;
  ruleVersion: string;
  vehicleCount: number;
  employeeCount: number;
  groups: string[];
  electricNetBonus: number;
  familyMinChildren: number;
  childMaxAge: number;
  sourceNote: string;
}
export interface Scenario {
  benefit?: {
    budget: number;
    monthlyCost: number;
    familyEmployerShare: number;
    eligibilityReason: string;
  } | null;
  carId: string | null;
  name: string;
  grossSalary: number;
  valueOfUse: number;
  maintenanceAllowance: number;
  familyEmployeeShare: number;
  salary: Salary;
}
export interface DecisionEvidence {
  outcome: string;
  inputKind: string;
  participationSource: string;
  ruleVersion: string;
  payrollTaxYear: number;
  sourceFingerprint: string;
  rules: string[];
  assumptions: string[];
  sources: { kind: string; name: string; sha256: string }[];
  vehicles: {
    scenario: string;
    carId: string;
    key: Vehicle["key"];
    valueOfUse: number;
    url: string;
  }[];
}
export interface Comparison {
  evidence?: DecisionEvidence | null;
  employeeId: string;
  current: Scenario;
  alternative: Scenario;
  netDifference: number;
  displayNetDifference: number;
  annualNetDifference: number;
  displayAnnualDifference: number;
  waterfall: {
    name: string;
    base: number;
    amount: number;
    change: number;
    total: boolean;
  }[];
}
export interface SalaryInputs {
  baseSalary: number;
  commission: number;
  bonus: number;
  pensionPercent: number;
  studyFundPercent: number;
  creditPoints: number;
}
export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
  }
}
export async function request<T>(
  url: string,
  signal?: AbortSignal,
  body?: unknown,
  extraHeaders?: Record<string, string>,
): Promise<T> {
  let response: Response;
  try {
    response = await fetch(url, {
      signal,
      ...(extraHeaders ? { headers: extraHeaders } : {}),
      ...(body === undefined
        ? {}
        : {
            method: "POST",
            headers: {
              ...extraHeaders,
              "Content-Type": "application/json",
              "X-Strauss-Client": "employee-web",
            },
            body: JSON.stringify(body),
          }),
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError")
      throw error;
    throw new ApiError("לא ניתן להתחבר לשרת. בדקו את החיבור ונסו שוב.", 0);
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    const fallback =
      response.status === 401 || response.status === 403
        ? "אין הרשאה לפעולה. יש להיכנס מחדש לתפקיד המתאים."
        : response.status === 409
          ? "הנתונים עודכנו בחלון אחר. יש לרענן ולנסות שוב."
          : response.status === 429
            ? "נשלחו בקשות רבות מדי. המתינו דקה ונסו שוב."
            : "הבקשה נכשלה. נסו שוב.";
    const detail = problem?.detail ?? problem?.title;
    throw new ApiError(
      typeof detail === "string" && detail.trim() ? detail : fallback,
      response.status,
    );
  }
  try {
    return (await response.json()) as T;
  } catch (error) {
    if (signal?.aborted) throw error;
    throw new ApiError("השרת החזיר תשובה לא תקינה. נסו לרענן את הנתונים.", 502);
  }
}
export const api = {
  context: (signal?: AbortSignal) => request<Context>("/api/context", signal),
  employees: (signal?: AbortSignal) =>
    request<Employee[]>("/api/employees", signal),
  vehicles: (id: string, signal?: AbortSignal) =>
    request<Vehicle[]>(
      `/api/vehicles?employeeId=${encodeURIComponent(id)}`,
      signal,
    ),
  compare: (
    employeeId: string,
    alternativeCarId: string | null,
    signal?: AbortSignal,
  ) =>
    request<Comparison>("/api/experience/compare", signal, {
      employeeId,
      alternativeCarId,
    }),
  simulate: (
    employeeId: string,
    alternativeCarId: string | null,
    inputs: SalaryInputs,
    signal?: AbortSignal,
  ) =>
    request<Comparison>("/api/experience/simulate", signal, {
      employeeId,
      alternativeCarId,
      inputs,
    }),
};
