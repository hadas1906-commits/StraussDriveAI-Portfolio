import type { ChatMessage } from "../advisor-api";
import { Tag } from "./ui";

export function ProviderStatus({
  message,
}: {
  message: Pick<ChatMessage, "mode" | "provider">;
}) {
  return (
    <>
      {message.mode === "deterministic-fallback" && (
        <Tag tone="amber">מענה דטרמיניסטי חלופי · לא נעשה שימוש בתוצאת AI</Tag>
      )}
      {message.mode === "llm" && message.provider?.outcome === "live_llm" && (
        <Tag tone="green">תשובת מודל חי התקבלה · החישוב במנוע הפיקטיבי</Tag>
      )}
      {message.mode === "llm" && !message.provider && (
        <Tag>הודעה שמורה · אבחון קריאת AI אינו זמין</Tag>
      )}
      {message.mode === "deterministic-demo" && (
        <Tag>דמו דטרמיניסטי · ללא קריאת AI</Tag>
      )}
      {message.provider && (
        <details className="provider-diagnostic">
          <summary>אבחון ספק AI ללא תוכן אישי</summary>
          <span dir="ltr">
            {message.provider.outcome} · HTTP{" "}
            {message.provider.httpStatus ?? "—"} · {message.provider.latencyMs}{" "}
            ms · {message.provider.attemptId}
          </span>
        </details>
      )}
    </>
  );
}
