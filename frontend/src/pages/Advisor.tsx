import { ComparisonSummary } from "../components/ComparisonSummary";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  Bot,
  Send,
  Plus,
  Clock3,
  ShieldCheck,
  MessageCircle,
  ChevronDown,
  UserRound,
  LoaderCircle,
  FileCheck2,
  X,
  ExternalLink,
} from "lucide-react";
import {
  advisorApi,
  employeeSession,
  localTime,
  statusLabel,
  type ChatView,
  type ChatMessage,
  type Facts,
  type Options,
  type Session,
  type ConversationSummary,
} from "../advisor-api";
import { useWorkspace } from "../workspace";
import { PageTitle, Loading, Tag } from "../components/ui";
import { ComparisonResult } from "./Comparison";
import { ProviderStatus } from "../components/ProviderStatus";
import { ConversationAssessment } from "../components/ConversationAssessment";
import { HandoffConfirmation } from "../components/HandoffConfirmation";
import { money } from "../lib/format";
const labels: Record<keyof Facts, string> = {
  occupation: "עיסוק",
  baseSalary: "שכר בסיס (₪)",
  commission: "עמלות (₪)",
  bonus: "בונוס (₪)",
  pensionPercent: "פנסיה עובד (%)",
  studyFundPercent: "קרן השתלמות (%)",
  creditPoints: "נקודות זיכוי",
  eligibleChildren: "ילדים עד גיל הזכאות",
  currentCar: "מסלול נוכחי",
  currentParticipation: "השתתפות נוכחית (₪)",
};
const numericFields = [
  "baseSalary",
  "commission",
  "bonus",
  "pensionPercent",
  "studyFundPercent",
  "creditPoints",
  "eligibleChildren",
  "currentParticipation",
] as const;
export function AdvisorPage() {
  const { employee } = useWorkspace();
  return (
    <>
      <PageTitle
        eyebrow="השיחה שמחברת בין כל המספרים"
        title="בואו נדבר על הנטו שלך"
        description="מבינים מה חסר, בודקים זכאות ומפעילים את מנוע השכר. נציג אנושי מצטרף רק כשצריך."
      />
      <AdvisorChat key={employee.id} />
    </>
  );
}
export function AssistantLauncher() {
  const { employee } = useWorkspace();
  const [open, setOpen] = useState(false);
  const launcher = useRef<HTMLButtonElement>(null);
  const popover = useRef<HTMLElement>(null);
  function close() {
    setOpen(false);
    requestAnimationFrame(() => launcher.current?.focus());
  }
  useEffect(() => {
    if (open)
      popover.current?.querySelector<HTMLButtonElement>("button")?.focus();
  }, [open]);
  return (
    <div className="assistant-launcher">
      {open && (
        <section
          ref={popover}
          id="floating-advisor"
          className="assistant-popover"
          aria-label="יועץ הרכב הצף"
          onKeyDown={(event) => {
            if (event.key === "Escape") {
              event.preventDefault();
              close();
            }
          }}
        >
          <div className="assistant-popover-header">
            <strong>
              <Bot size={18} />
              יועץ הרכב שלך
            </strong>
            <Link
              to="/advisor"
              onClick={() => setOpen(false)}
              aria-label="פתיחת שיחה בעמוד מלא"
            >
              <ExternalLink size={16} />
            </Link>
            <button
              className="icon-button"
              aria-label="סגירת היועץ"
              onClick={close}
            >
              <X size={18} />
            </button>
          </div>
          <AdvisorChat compact key={employee.id} />
        </section>
      )}
      <button
        ref={launcher}
        className="assistant-fab"
        onClick={() => setOpen(!open)}
        aria-expanded={open}
        aria-controls="floating-advisor"
      >
        <MessageCircle size={21} />
        {open ? "סגירת היועץ" : "נבדוק מה יקרה לנטו?"}
      </button>
    </div>
  );
}
export function AdvisorChat({ compact = false }: { compact?: boolean }) {
  const { employee } = useWorkspace();
  const [params, setParams] = useSearchParams();
  // A dashboard selection always opens that verified profile. Custom mode is an
  // explicit, employee-scoped URL choice, never an invisible localStorage override.
  const profile =
    params.get("profile") === `custom:${employee.id}` ? null : employee.id;
  const [session, setSession] = useState<Session | null>(null),
    [options, setOptions] = useState<Options | null>(null),
    [history, setHistory] = useState<ConversationSummary[]>([]),
    [view, setView] = useState<ChatView | null>(null);
  const [text, setText] = useState(""),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [loading, setLoading] = useState(true),
    [share, setShare] = useState(false),
    [attempt, setAttempt] = useState(0);
  const [signedOut, setSignedOut] = useState(() => {
    try {
      return sessionStorage.getItem("sd.employee.signedOut") === "true";
    } catch {
      return false;
    }
  });
  const [historyExpanded, setHistoryExpanded] = useState(false);
  const revision = useRef(0),
    sending = useRef(false),
    bottom = useRef<HTMLDivElement>(null),
    input = useRef<HTMLTextAreaElement>(null),
    current = useRef<ChatView | null>(null);
  current.current = view;
  useEffect(() => {
    setShare(false);
  }, [view?.conversation.id, view?.ticket?.id, view?.conversation.state.stage]);
  useEffect(() => {
    let active = true;
    const epoch = ++revision.current;
    setView(null);
    setHistory([]);
    setError("");
    setLoading(true);
    setSession(null);
    setShare(false);
    setText("");
    if (signedOut) {
      setLoading(false);
      return;
    }
    employeeSession(profile)
      .then(async (auth) => {
        if (!active) return;
        setSession(auth);
        const [opts, list] = await Promise.all([
          advisorApi.options(),
          advisorApi.list(),
        ]);
        if (!active) return;
        setOptions(opts);
        setHistory(list);
        const requested = params.get("conversation");
        if (requested && !list.some((c) => c.id === requested))
          throw new Error("השיחה המבוקשת אינה בחשבון הפעיל.");
        const next = requested ?? list[0]?.id;
        const loaded = next
          ? await advisorApi.view(next)
          : await advisorApi.create();
        if (active && epoch === revision.current) setView(loaded);
      })
      .catch((e) => {
        if (active) setError(e.message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
      revision.current++;
    };
  }, [profile, employee.id, attempt, params, signedOut]);
  useEffect(() => {
    if (!view?.ticket) return;
    let active = true;
    const timer = setInterval(async () => {
      if (sending.current || !current.current) return;
      const epoch = revision.current;
      try {
        const fresh = await advisorApi.view(current.current.conversation.id);
        if (active && epoch === revision.current) setView(fresh);
      } catch (e) {
        if (active)
          setError(
            e instanceof Error ? e.message : "לא ניתן לרענן את מצב הפנייה.",
          );
      }
    }, 2500);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [view?.ticket?.id]);
  const last = view?.messages.at(-1)?.id;
  useEffect(() => {
    bottom.current?.scrollIntoView({ block: "nearest" });
  }, [last, busy]);
  async function send(
    message: string,
    facts?: Partial<Facts>,
    shareMessage = false,
    confirmHandoff?: string,
    requestCancellation = false,
    factDecision?: "confirm" | "discard",
  ) {
    if (!view || sending.current || !message.trim()) return;
    const epoch = ++revision.current;
    sending.current = true;
    setBusy(true);
    setError("");
    try {
      const next = await advisorApi.message(
        view.conversation.id,
        message,
        view.conversation.version,
        facts,
        shareMessage &&
          share &&
          view.conversation.state.stage === "AwaitingClarification",
        confirmHandoff,
        requestCancellation,
        factDecision,
      );
      if (epoch === revision.current) {
        setView(next);
        setText("");
        setHistoryExpanded(false);
        setShare(false);
        setHistory(await advisorApi.list());
        input.current?.focus();
      }
    } catch (e) {
      if (epoch === revision.current) {
        setError(e instanceof Error ? e.message : "השליחה נכשלה.");
        if ((e as { status?: number }).status === 409) {
          try {
            const fresh = await advisorApi.view(view.conversation.id);
            if (epoch === revision.current) setView(fresh);
          } catch {
            // Keep the original conflict and draft visible; the user can retry refresh.
          }
        }
      }
    } finally {
      sending.current = false;
      if (epoch === revision.current) setBusy(false);
    }
  }
  async function choose(id?: string) {
    if (sending.current) return;
    const epoch = ++revision.current;
    setLoading(true);
    setError("");
    setShare(false);
    try {
      const next = id ? await advisorApi.view(id) : await advisorApi.create();
      if (epoch === revision.current) {
        setView(next);
        setText("");
        setHistoryExpanded(false);
        setHistory(await advisorApi.list());
      }
    } catch (e) {
      if (epoch === revision.current)
        setError(e instanceof Error ? e.message : "טעינה נכשלה");
    } finally {
      if (epoch === revision.current) setLoading(false);
    }
  }
  function switchProfile(custom: boolean) {
    setParams(custom ? { profile: `custom:${employee.id}` } : {});
  }
  async function signOut() {
    if (sending.current) return;
    sending.current = true;
    setBusy(true);
    revision.current++;
    try {
      await advisorApi.employeeSignOut();
      try {
        sessionStorage.setItem("sd.employee.signedOut", "true");
      } catch {}
      setSignedOut(true);
      setView(null);
      setHistory([]);
      setSession(null);
      setOptions(null);
      setText("");
      setError("");
      setShare(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : "היציאה נכשלה");
    } finally {
      sending.current = false;
      setBusy(false);
    }
  }
  function reenter() {
    try {
      sessionStorage.removeItem("sd.employee.signedOut");
    } catch {}
    setParams(profile === null ? { profile: `custom:${employee.id}` } : {});
    setSignedOut(false);
  }
  return (
    <div className={`advisor-layout ${compact ? "compact" : ""}`}>
      {!compact && (
        <aside className="panel chat-history">
          <div className="section-title">
            <h2>השיחות שלי</h2>
            <button
              className="icon-button history-disclosure-toggle"
              aria-label="פתיחת היסטוריית השיחות"
              aria-expanded={historyExpanded}
              aria-controls="advisor-history-content"
              onClick={() => setHistoryExpanded((value) => !value)}
            >
              <ChevronDown size={19} />
            </button>
            <button
              className="icon-button"
              aria-label="שיחה חדשה"
              disabled={busy || loading || !session}
              onClick={() => choose()}
            >
              <Plus size={19} />
            </button>
          </div>
          <p>היסטוריה פרטית לחשבון ההדגמה הפעיל</p>
          <div className="chat-profile-toggle">
            <button
              disabled={busy || profile === employee.id}
              onClick={() => switchProfile(false)}
            >
              עובד ההדגמה הפעיל
            </button>
            <button
              disabled={busy || profile === null}
              onClick={() => switchProfile(true)}
            >
              פרופיל חדש משלי
            </button>
          </div>
          <div
            id="advisor-history-content"
            className={`history-content ${historyExpanded ? "expanded" : ""}`}
          >
            {history.map((c) => (
              <button
                key={c.id}
                className={`history-row ${c.id === view?.conversation.id ? "active" : ""}`}
                aria-pressed={c.id === view?.conversation.id}
                disabled={busy}
                onClick={() => choose(c.id)}
              >
                <MessageCircle size={16} />
                <span>
                  <strong>{c.title}</strong>
                  <small>
                    {statusLabel(c.stage)} · {localTime(c.updatedAt)}
                  </small>
                </span>
              </button>
            ))}
            <div className="privacy-note">
              <ShieldCheck size={18} />
              שינוי עובד ההדגמה פותח זהות נפרדת. שיחות ושכר אינם מועברים לחשבון
              אחר. פרופיל אישי נשאר נפרד. לחזרה לנתונים המאומתים בחרו עובד
              ההדגמה הפעיל.
            </div>
          </div>
        </aside>
      )}
      <section className="chat-panel panel">
        <div className="chat-toolbar">
          <div>
            <Bot size={23} />
            <div>
              <strong>יועץ הרכב של StraussDrive</strong>
              <span>
                {profile === null
                  ? "פרופיל אישי חדש — נתונים פרטיים לשיחה"
                  : `הדגמה: ${employee.name}`}
              </span>
              {profile !== null && (
                <small>
                  קבוצה מאומתת {employee.group} · רכב נוכחי{" "}
                  <bdi>{employee.currentCar}</bdi>
                </small>
              )}
            </div>
          </div>
          <Tag tone={session?.agentMode === "llm" ? "green" : "amber"}>
            {session?.agentMode === "llm"
              ? "מודל שפה מוגדר"
              : "דמו דטרמיניסטי · ללא LLM"}
          </Tag>
          {session && (
            <button
              className="text-link"
              disabled={busy || loading}
              onClick={() => void signOut()}
            >
              יציאה מהשיחות הפרטיות
            </button>
          )}
        </div>
        <p className="chat-auth-note">
          אימות תפקידי הדגמה בלבד · עובדים פיקטיביים או נתונים שהזנת · אין
          להשתמש במידע אמיתי בסביבה ציבורית. יציאה מהשיחות מבטלת את הגישה
          לחשבונות שנזכרו בדפדפן; כניסה מחדש יוצרת חשבון הדגמה נפרד
        </p>
        {signedOut ? (
          <section className="state-box" aria-label="יציאה מהחשבון">
            <ShieldCheck size={28} />
            <strong>יצאת מהשיחות הפרטיות</strong>
            <p>
              הגישה לחשבונות שנזכרו בדפדפן בוטלה. הרשומות נשמרו בשרת; כניסה חדשה
              יוצרת חשבון הדגמה נפרד.
            </p>
            <button className="btn primary" onClick={reenter}>
              כניסה לחשבון הדגמה חדש
            </button>
          </section>
        ) : loading ? (
          <Loading label="פותחים את השיחה השמורה…" />
        ) : (
          <>
            <div
              className="message-feed"
              role="log"
              aria-label="היסטוריית שיחה"
            >
              {view?.messages.map((message) => (
                <MessageBubble
                  key={message.id}
                  message={message}
                  compact={compact}
                />
              ))}
              {busy && (
                <div className="typing-indicator" role="status">
                  <LoaderCircle className="spin" size={16} />
                  בודקים את הנתונים ומפעילים כלים…
                </div>
              )}
              <div ref={bottom} />
            </div>
            {view?.ticket && (
              <div className="ticket-waiting" role="status">
                <Clock3 size={18} />
                <div>
                  <strong>
                    {statusLabel(view.ticket.status)} · פנייה{" "}
                    {view.ticket.id.slice(0, 8)}
                  </strong>
                  <p>השלב נשמר בשרת. תשובת נציג תופיע כאן גם אחרי רענון.</p>
                  <button
                    className="text-link"
                    disabled={busy}
                    onClick={() =>
                      send(
                        "בקשת סגירת הפנייה",
                        undefined,
                        false,
                        undefined,
                        true,
                      )
                    }
                  >
                    בקשת סגירת הפנייה
                  </button>
                </div>
              </div>
            )}
            {!!view?.ticketHistory?.length && (
              <details className="employee-ticket-history">
                <summary>
                  היסטוריית פניות לנציג ({view.ticketHistory.length})
                </summary>
                {view.ticketHistory.map(({ ticket, responses }) => (
                  <article key={ticket.id}>
                    <strong>
                      פנייה {ticket.id.slice(0, 8)} ·{" "}
                      {statusLabel(ticket.status)}
                    </strong>
                    <p>{ticket.question}</p>
                    <small>עודכנה {localTime(ticket.updatedAt)}</small>
                    {responses.map((response) => (
                      <div key={response.id}>
                        <p>
                          {response.actorRole === "employee"
                            ? "הבהרה ששיתפת"
                            : "תשובת נציג"}
                          : {response.text}
                        </p>
                        {response.amount !== null && (
                          <p>השתתפות בסיסית מאושרת: {money(response.amount)}</p>
                        )}
                        {response.effectiveFrom && (
                          <p>בתוקף מ־{response.effectiveFrom}</p>
                        )}
                      </div>
                    ))}
                  </article>
                ))}
              </details>
            )}
            {error && (
              <div className="chat-error" role="alert">
                {error}
                <button
                  className="text-link"
                  onClick={() => setAttempt((n) => n + 1)}
                >
                  רענון השיחה
                </button>
              </div>
            )}
            <ConversationAssessment assessment={view?.assessment} />
            {view?.conversation.state.proposedFacts && (
              <section className="facts-confirmation">
                <strong>נתונים שהמודל זיהה — לא שימשו לחישוב עדיין</strong>
                <dl>
                  {Object.entries(view.conversation.state.proposedFacts)
                    .filter(([, value]) => value !== null)
                    .map(([key, value]) => (
                      <div key={key}>
                        <dt>{labels[key as keyof Facts]}</dt>
                        <dd>{String(value)}</dd>
                      </div>
                    ))}
                </dl>
                <button
                  className="btn primary"
                  disabled={busy}
                  onClick={() =>
                    send(
                      "מאשר/ת את הנתונים",
                      undefined,
                      false,
                      undefined,
                      false,
                      "confirm",
                    )
                  }
                >
                  אישור הנתונים
                </button>
                <button
                  className="btn secondary"
                  disabled={busy}
                  onClick={() =>
                    send(
                      "לא מאשר את הנתונים",
                      undefined,
                      false,
                      undefined,
                      false,
                      "discard",
                    )
                  }
                >
                  ביטול הנתונים שהוצעו
                </button>
              </section>
            )}
            {view?.conversation.state.proposedHandoff && (
              <HandoffConfirmation
                id={view.conversation.state.proposedHandoff.id}
                kind={view.conversation.state.proposedHandoff.kind}
                busy={busy}
                confirm={(id) =>
                  send(
                    view.conversation.state.proposedHandoff?.kind === "cancel"
                      ? "אישור סגירת הפנייה"
                      : "אישור העברת בקשה לנציג",
                    undefined,
                    false,
                    id,
                  )
                }
              />
            )}
            {options && view && (
              <details className="chat-facts">
                <summary>
                  <FileCheck2 size={16} />
                  נתונים ידועים ותיקון פרטים
                  <ChevronDown size={14} />
                </summary>
                <FactsForm
                  key={`${view.conversation.id}-${view.conversation.version}`}
                  facts={view.conversation.state.facts}
                  options={options}
                  verifiedProfile={profile !== null}
                  disabled={busy}
                  save={(facts) => send("עדכון נתונים שהזנתי בטופס", facts)}
                />
              </details>
            )}
            <div className="chat-suggestions">
              {[
                "בדיקת רכב חשמלי",
                "בדיקת מסלול משפחתי",
                "בדיקת ויתור על הרכב",
                "פירוט השכר הנוכחי",
              ].map((suggestion) => (
                <button
                  disabled={busy || !view}
                  key={suggestion}
                  onClick={() => send(suggestion)}
                >
                  {suggestion}
                </button>
              ))}
            </div>
            {view?.conversation.state.stage === "AwaitingClarification" && (
              <label className="share-clarification">
                <input
                  type="checkbox"
                  checked={share}
                  onChange={(e) => setShare(e.target.checked)}
                />
                תוכן ההודעה הבאה יועבר לנציג בהסכמתי. אין לכלול שכר או מידע
                אישי.
              </label>
            )}
            <form
              className="chat-composer"
              onSubmit={(e: FormEvent) => {
                e.preventDefault();
                void send(text, undefined, true);
              }}
            >
              <label
                className="sr-only"
                htmlFor={compact ? "assistant-text-compact" : "assistant-text"}
              >
                הודעה ליועץ
              </label>
              <textarea
                id={compact ? "assistant-text-compact" : "assistant-text"}
                ref={input}
                value={text}
                maxLength={4000}
                rows={2}
                placeholder="למשל: אם אעבור ל־MG4, מה יקרה לנטו שלי?"
                disabled={busy || !view}
                onChange={(e) => setText(e.target.value)}
                onKeyDown={(e) => {
                  if (
                    e.key === "Enter" &&
                    !e.shiftKey &&
                    !e.nativeEvent.isComposing
                  ) {
                    e.preventDefault();
                    void send(text, undefined, true);
                  }
                }}
              />
              <button
                type="submit"
                className="btn primary"
                aria-label="שליחת הודעה"
                disabled={!text.trim() || busy || !view}
              >
                <Send size={19} />
              </button>
            </form>
            <p className="chat-footnote">
              רק מנוע .NET מחשב שכר וזכאות.{" "}
              {session?.agentMode === "llm"
                ? "המודל מתכנן בדיקות באמצעות כלים מאומתים; שינויים שזוהו בו דורשים אישור."
                : "הדמו משתמש בכללים מפורשים לזיהוי הודעות, לא במודל שפה."}
            </p>
          </>
        )}
        {!loading && !view && (
          <button
            className="btn secondary"
            onClick={() => setAttempt((n) => n + 1)}
          >
            ניסיון נוסף
          </button>
        )}
      </section>
    </div>
  );
}
function MessageBubble({
  message,
  compact,
}: {
  message: ChatMessage;
  compact: boolean;
}) {
  return (
    <article className={`chat-message ${message.role}`}>
      <div className="message-meta">
        {message.role === "employee" ? (
          <UserRound size={15} />
        ) : message.role === "representative" ? (
          <ShieldCheck size={15} />
        ) : (
          <Bot size={15} />
        )}
        <strong>
          {message.role === "employee"
            ? "את/ה"
            : message.role === "representative"
              ? "נציג אנושי"
              : "יועץ הרכב"}
        </strong>
        <time dateTime={message.createdAt}>{localTime(message.createdAt)}</time>
        <ProviderStatus message={message} />
      </div>
      {message.summary && <ComparisonSummary summary={message.summary} />}
      {!message.summary?.tableOnly && <p>{message.text}</p>}
      {compact && message.summary && !message.summary.tableOnly && (
        <Link to="/advisor">לפירוט השיחה והתרשים</Link>
      )}
      {!message.summary?.tableOnly &&
        !(compact && message.summary) &&
        (
          message.comparisons ??
          (message.comparison ? [message.comparison] : [])
        ).map((comparison, index) =>
          compact ? (
            <div className="chat-mini-result" key={index}>
              <span>
                היום {money(comparison.current.salary.displayNetSalary)}
              </span>
              <span>
                בחלופה {money(comparison.alternative.salary.displayNetSalary)}
              </span>
              <strong>
                הפרש {money(comparison.displayNetDifference)} לחודש
              </strong>
              <Link to="/advisor">לפירוט השיחה והתרשים</Link>
            </div>
          ) : message.summary ? (
            <details
              className="chat-calculation comparison-details"
              key={comparison.alternative.carId ?? "waiver"}
            >
              <summary>
                פירוט החישוב — {comparison.alternative.name}{" "}
                <bdi>{comparison.alternative.carId}</bdi>
              </summary>
              <ComparisonResult result={comparison} simulated={false} />
            </details>
          ) : (
            <div
              className="chat-calculation"
              key={comparison.alternative.carId ?? "waiver"}
            >
              <ComparisonResult result={comparison} simulated={false} />
            </div>
          ),
        )}
      {message.breakdown && (
        <dl className="chat-breakdown">
          {Object.entries(message.breakdown).map(([key, value]) => (
            <div key={key}>
              <dt>
                {
                  (
                    {
                      grossCashSalary: "שכר במזומן",
                      taxableIncome: "הכנסה חייבת",
                      incomeTax: "מס הכנסה",
                      nationalInsurance: "ביטוח לאומי",
                      healthInsurance: "בריאות",
                      employeePension: "פנסיה",
                      studyFund: "קרן השתלמות",
                      employeeNetParticipation: "השתתפות",
                      electricReimbursementGross: "גילום חשמל",
                      netSalary: "נטו ללא עיגול",
                      displayNetSalary: "נטו מוצג",
                    } as Record<string, string>
                  )[key]
                }
              </dt>
              <dd>{money(value)}</dd>
            </div>
          ))}
        </dl>
      )}
      {message.tools.length > 0 && (
        <details className="tool-trace">
          <summary>כלים שהופעלו במנוע החישוב</summary>
          {message.tools.map((tool, index) => (
            <code key={index} dir="ltr">
              {tool.name}
            </code>
          ))}
        </details>
      )}
      {message.sources.length > 0 && (
        <div className="message-sources">
          {message.sources.map((source) => (
            <a
              key={source.url}
              href={source.url}
              target="_blank"
              rel="noreferrer noopener"
            >
              <ExternalLink size={12} />
              {source.label}
            </a>
          ))}
        </div>
      )}
    </article>
  );
}
function FactsForm({
  facts,
  options,
  disabled,
  save,
  verifiedProfile,
}: {
  facts: Facts;
  options: Options;
  disabled: boolean;
  save: (facts: Partial<Facts>) => void;
  verifiedProfile: boolean;
}) {
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      Object.entries(facts).map(([key, value]) => [
        key,
        value === null ? "" : String(value),
      ]),
    ),
  );
  function submit(e: FormEvent) {
    e.preventDefault();
    const patch: Record<string, unknown> = {};
    for (const key of Object.keys(labels)) {
      const value = values[key] ?? "";
      if (value === "") continue;
      patch[key] = numericFields.includes(key as (typeof numericFields)[number])
        ? Number(value)
        : value;
    }
    save(patch as Partial<Facts>);
  }
  return (
    <form onSubmit={submit} className="facts-form">
      <p>
        מעדכנים רק שדות שמולאו. ערך ריק אינו מאפס נתון קיים; יש לציין 0 במפורש
        כשאין רכיב.
      </p>
      <label>
        עיסוק מהמקור
        <select
          aria-label="עיסוק לשיחה"
          disabled={disabled || verifiedProfile}
          value={values.occupation ?? ""}
          onChange={(e) => setValues({ ...values, occupation: e.target.value })}
        >
          <option value="">בחירת עיסוק…</option>
          {options.occupations.map((o) => (
            <option key={o.name} value={o.name}>
              {o.name} · קבוצה {o.group}
            </option>
          ))}
        </select>
        {verifiedProfile && (
          <small>
            העיסוק והזכאות נקבעים מפרופיל העובד המאומת. לפרופיל אחר יש לבחור
            במפורש פרופיל אישי נפרד.
          </small>
        )}
      </label>
      <div className="input-grid">
        {numericFields.map((field) => (
          <label key={field}>
            {labels[field]}
            <input
              aria-label={`נתון לשיחה: ${labels[field]}`}
              type="number"
              min={0}
              step={field === "eligibleChildren" ? 1 : "0.01"}
              max={
                field.endsWith("Percent") ||
                field === "eligibleChildren" ||
                field === "creditPoints"
                  ? 100
                  : field === "currentParticipation"
                    ? 1000000
                    : 100000000
              }
              dir="ltr"
              disabled={disabled}
              value={values[field] ?? ""}
              onChange={(e) =>
                setValues({ ...values, [field]: e.target.value })
              }
            />
          </label>
        ))}
      </div>
      <label>
        מסלול נוכחי
        <select
          aria-label="מסלול נוכחי לשיחה"
          disabled={disabled}
          value={values.currentCar ?? ""}
          onChange={(e) => setValues({ ...values, currentCar: e.target.value })}
        >
          <option value="">בחירת מסלול…</option>
          <option value="waiver">ויתור / אין רכב חברה</option>
          {options.vehicles.map((v) => (
            <option key={v.id} value={v.id}>
              {v.name} · {v.id}
            </option>
          ))}
        </select>
      </label>
      <button className="btn secondary" disabled={disabled}>
        עדכון הנתונים והמשך
      </button>
    </form>
  );
}
