import { request, type Comparison, type Salary, type Scenario } from "./api";
export interface Facts {
  occupation: string | null;
  baseSalary: number | null;
  commission: number | null;
  bonus: number | null;
  pensionPercent: number | null;
  studyFundPercent: number | null;
  creditPoints: number | null;
  eligibleChildren: number | null;
  currentCar: string | null;
  currentParticipation: number | null;
}
export interface SourceLink {
  label: string;
  url: string;
}
export interface ComparisonSummary {
  rows: {
    vehicleId: string;
    name: string;
    baseline: boolean;
    status: string;
    explanation: string;
    monthlyNet: number | null;
    monthlyDifference: number | null;
    annualDifference: number | null;
  }[];
  recommendation: string | null;
  tableOnly: boolean;
}
export interface ChatMessage {
  baseline?: Scenario | null;
  summary?: ComparisonSummary | null;
  comparisons?: Comparison[] | null;
  id: string;
  role: string;
  text: string;
  mode: string;
  provider?: {
    outcome: string;
    httpStatus: number | null;
    latencyMs: number;
    attemptId: string;
  } | null;
  createdAt: string;
  comparison: Comparison | null;
  breakdown: Salary | null;
  tools: { name: string; sources: string[] }[];
  sources: SourceLink[];
}
export interface ChatState {
  proposedHandoff?: {
    kind?: "create" | "cancel";
    id: string;
    category: string;
    vehicleId: string | null;
    target: string;
  } | null;
  verifiedGroup?: string | null;
  stage: string;
  facts: Facts;
  alternative: string | null;
  intent: string;
  pendingField: string | null;
  pendingTicketId: string | null;
  proposedFacts: Facts | null;
}
export interface Conversation {
  id: string;
  ownerId: string;
  profileKey: string;
  title: string;
  state: ChatState;
  version: number;
  createdAt: string;
  updatedAt: string;
}
export interface Ticket {
  id: string;
  conversationId: string;
  status: string;
  category: string;
  question: string;
  vehicleId: string | null;
  group: string;
  occupation: string;
  createdAt: string;
  updatedAt: string;
  version: number;
  target: string;
}
export interface Response {
  effectiveFrom?: string | null;
  id: string;
  ticketId: string;
  actorRole: string;
  action: string;
  text: string;
  amount: number | null;
  createdAt: string;
}
export interface EmployeeTicketHistory {
  ticket: Ticket;
  responses: Response[];
}
export interface Assessment {
  status: string;
  missingFields: string[];
  conflicts: string[];
  inputs: { field: string; source: string; status: string }[];
  nextStep: string;
}
export interface ChatView {
  assessment?: Assessment | null;
  ticketHistory?: EmployeeTicketHistory[];
  conversation: Conversation;
  messages: ChatMessage[];
  ticket: Ticket | null;
  responses: Response[];
}
export interface TicketView {
  ticket: Ticket;
  responses: Response[];
  audit: { id: string; actorRole: string; action: string; createdAt: string }[];
}
export interface ConversationSummary {
  id: string;
  title: string;
  profileKey: string;
  stage: string;
  updatedAt: string;
  version: number;
}
export interface Session {
  id: string;
  role: string;
  profileKey: string;
  demoAuthentication: boolean;
  agentMode: string;
}
export interface Options {
  occupations: {
    name: string;
    group: string;
    mandatoryCar: boolean;
    canWaive: boolean;
  }[];
  vehicles: { id: string; name: string; drive: string; group: string }[];
  payrollTaxYear: number;
  childMaxAge: number;
}
let sessionChain: Promise<unknown> = Promise.resolve();
let pending: { profile: string | null; promise: Promise<Session> } | null =
  null;
export function employeeSession(profile: string | null): Promise<Session> {
  if (pending?.profile === profile) return pending.promise;
  const promise = sessionChain
    .catch(() => {})
    .then(() =>
      request<Session>("/api/advisor/employee-session", undefined, {
        sampleEmployeeId: profile,
      }),
    );
  sessionChain = promise;
  pending = { profile, promise };
  promise
    .finally(() => {
      if (pending?.promise === promise) pending = null;
    })
    .catch(() => {});
  return promise;
}
export const advisorApi = {
  employeeSignOut: () => {
    const promise = sessionChain
      .catch(() => {})
      .then(() =>
        request<{ signedOut: boolean }>("/api/advisor/sign-out", undefined, {}),
      );
    sessionChain = promise;
    pending = null;
    return promise;
  },
  representativeSignOut: () =>
    request<{ signedOut: boolean }>(
      "/api/representative/sign-out",
      undefined,
      {},
    ),
  mode: () =>
    request<{ agentMode: string; demoAuthentication: boolean }>(
      "/api/advisor/mode",
    ),
  options: () => request<Options>("/api/advisor/options"),
  list: () => request<ConversationSummary[]>("/api/advisor/conversations/"),
  create: () => request<ChatView>("/api/advisor/conversations/", undefined, {}),
  view: (id: string) =>
    request<ChatView>(`/api/advisor/conversations/${encodeURIComponent(id)}`),
  message: (
    id: string,
    text: string,
    version: number,
    facts?: Partial<Facts>,
    share = false,
    confirmHandoff?: string,
    requestCancellation = false,
    factDecision?: "confirm" | "discard",
  ) =>
    request<ChatView>(
      `/api/advisor/conversations/${encodeURIComponent(id)}/messages`,
      undefined,
      {
        text,
        expectedVersion: version,
        facts,
        shareWithRepresentative: share,
        ...(confirmHandoff ? { confirmHandoff } : {}),
        ...(requestCancellation ? { requestCancellation: true } : {}),
        ...(factDecision ? { factDecision } : {}),
      },
    ),
  representativeStatus: () =>
    request<{ authenticated: boolean }>("/api/representative/session"),
  representativeSession: (code: string) =>
    request<Session>("/api/representative/session", undefined, {
      code: code || null,
    }),
  tickets: () => request<Ticket[]>("/api/representative/tickets/"),
  ticket: (id: string) =>
    request<TicketView>(
      `/api/representative/tickets/${encodeURIComponent(id)}`,
    ),
  respond: (
    ticket: Ticket,
    action: string,
    text: string,
    amount: number | null,
    effectiveFrom?: string,
  ) =>
    request<TicketView>(
      `/api/representative/tickets/${encodeURIComponent(ticket.id)}/actions`,
      undefined,
      {
        action,
        text,
        expectedVersion: ticket.version,
        participationAmount: amount,
        ...(effectiveFrom ? { effectiveFrom } : {}),
      },
    ),
};
export const localTime = (value: string) =>
  new Intl.DateTimeFormat("he-IL", {
    dateStyle: "short",
    timeStyle: "short",
    timeZone: "Asia/Jerusalem",
  }).format(new Date(value));
export const statusLabel = (value: string) =>
  ({
    Open: "פתוחה",
    ClarificationRequested: "ממתינה להבהרת עובד",
    Answered: "נענתה",
    Closed: "נסגרה",
    Superseded: "החלופה השתנתה",
    WaitingForRepresentative: "ממתינה לנציג",
    AwaitingClarification: "נדרשת הבהרה",
    Completed: "החישוב הושלם",
    GatheringFacts: "השלמת נתונים",
    SelectingAlternative: "בחירת חלופה",
    ConfirmingFacts: "אישור נתונים",
  })[value] ?? value;
