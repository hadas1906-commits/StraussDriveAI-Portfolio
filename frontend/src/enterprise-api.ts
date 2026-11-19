import { request, type Comparison, type SalaryInputs } from "./api";
export interface LabRequest {
  name: string;
  alternativeCarId: string | null;
  inputs: SalaryInputs;
}
export interface LabResult {
  id: string | null;
  name: string;
  status: string;
  nextStep: string;
  createdAt: string | null;
  request: LabRequest;
  comparison: Comparison | null;
}
export interface RuntimeQuality {
  assistantTurns: number;
  providerAttempts: number;
  providerResponses: number;
  fallbackTurns: number;
  demoTurns: number;
  toolCalls: number;
  completedToolCalls: number;
  failedToolCalls: number;
  needsInputToolCalls: number;
  comparisonsProduced: number;
  medianProviderLatencyMs: number | null;
  groundednessEvaluated: boolean;
}
export interface QualityView {
  scope: string;
  runtime: RuntimeQuality;
  limitations: string[];
  evaluation: {
    codeCommit?: string | null;
    schemaVersion: number;
    status: string;
    runId: string | null;
    recordedAt: string | null;
    sourceFingerprint: string | null;
    live: null;
    scripted: {
      scenarios: number;
      completed: number;
      grounded: number;
      unnecessaryQuestions: number;
      unsafeOutcomes: number;
      handoffJourneys: number;
      persistedHandoffs: number;
      medianToolLatencyMs: number | null;
    } | null;
  };
}
export const labApi = (profile: string) => {
  const headers = { "X-Strauss-Profile": profile };
  return {
    list: () =>
      request<LabResult[]>(
        "/api/advisor/scenarios/",
        undefined,
        undefined,
        headers,
      ),
    preview: (scenarios: LabRequest[]) =>
      request<LabResult[]>(
        "/api/advisor/scenarios/preview",
        undefined,
        { scenarios },
        headers,
      ),
    save: (scenario: LabRequest) =>
      request<LabResult>(
        "/api/advisor/scenarios/",
        undefined,
        scenario,
        headers,
      ),
    remove: (id: string) =>
      request<{ removed: boolean }>(
        `/api/advisor/scenarios/${encodeURIComponent(id)}/remove`,
        undefined,
        {},
        headers,
      ),
  };
};
export const qualityApi = () => request<QualityView>("/api/advisor/quality");
