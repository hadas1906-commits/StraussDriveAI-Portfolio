import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  ShieldCheck,
  Inbox,
  RefreshCw,
  CheckCircle2,
  MessageSquare,
  LockKeyhole,
} from "lucide-react";
import {
  advisorApi,
  localTime,
  statusLabel,
  type Ticket,
  type TicketView,
} from "../advisor-api";
import { PageTitle, Tag } from "../components/ui";
import { ApiError } from "../api";
const today = () =>
  new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Jerusalem",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
export function RepresentativePage() {
  const [tickets, setTickets] = useState<Ticket[]>([]),
    [selected, setSelected] = useState<TicketView | null>(null),
    [ready, setReady] = useState(false),
    [loading, setLoading] = useState(true),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [code, setCode] = useState(""),
    [text, setText] = useState(""),
    [amount, setAmount] = useState(""),
    [effectiveFrom, setEffectiveFrom] = useState(today),
    [action, setAction] = useState("answer");
  const generation = useRef(0),
    mounted = useRef(true);
  const choosing = useRef(false);
  const selectedRef = useRef(selected),
    readyRef = useRef(ready),
    busyRef = useRef(busy),
    dirtyRef = useRef(false);
  selectedRef.current = selected;
  readyRef.current = ready;
  busyRef.current = busy;
  dirtyRef.current = !!(text || amount || effectiveFrom !== today());
  function clearSession(message = "") {
    setReady(false);
    setSelected(null);
    setTickets([]);
    setText("");
    setAmount("");
    setEffectiveFrom(today());
    setError(message);
  }
  function unauthorized(error: unknown) {
    if (
      error instanceof ApiError &&
      (error.status === 401 || error.status === 403)
    ) {
      clearSession("ההרשאה לתפקיד נציג הסתיימה. יש להיכנס מחדש.");
      return true;
    }
    return false;
  }
  async function refresh(preserveDraft = true) {
    if (choosing.current) return;
    const epoch = ++generation.current;
    const selectedId = selectedRef.current?.ticket.id;
    try {
      const session = await advisorApi.representativeStatus();
      if (!mounted.current || epoch !== generation.current) return;
      if (!session.authenticated) {
        clearSession(
          readyRef.current ? "ההרשאה לתפקיד נציג הסתיימה. יש להיכנס מחדש." : "",
        );
        return;
      }
      const list = await advisorApi.tickets();
      if (!mounted.current || epoch !== generation.current) return;
      setTickets(list);
      setReady(true);
      if (!preserveDraft) setError("");
      if (selectedId && !(preserveDraft && dirtyRef.current)) {
        const current = await advisorApi.ticket(selectedId);
        if (mounted.current && epoch === generation.current)
          setSelected(current);
      }
    } catch (e) {
      if (mounted.current && epoch === generation.current) {
        if (!unauthorized(e))
          setError(e instanceof Error ? e.message : "הטעינה נכשלה");
      }
    } finally {
      if (mounted.current && epoch === generation.current) setLoading(false);
    }
  }
  useEffect(() => {
    mounted.current = true;
    void refresh();
    return () => {
      mounted.current = false;
      generation.current++;
    };
  }, []);
  useEffect(() => {
    if (!ready) return;
    const timer = setInterval(() => {
      if (!busyRef.current) void refresh();
    }, 5000);
    return () => clearInterval(timer);
  }, [ready]);
  async function login(e: FormEvent) {
    e.preventDefault();
    setError("");
    setBusy(true);
    try {
      await advisorApi.representativeSession(code);
      setCode("");
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "הכניסה נכשלה");
    } finally {
      setBusy(false);
    }
  }
  async function choose(ticket: Ticket) {
    const epoch = ++generation.current;
    choosing.current = true;
    setSelected(null);
    setError("");
    try {
      const next = await advisorApi.ticket(ticket.id);
      if (!mounted.current || epoch !== generation.current) return;
      setSelected(next);
      setText("");
      setAmount("");
      setEffectiveFrom(today());
      setAction("answer");
    } catch (e) {
      if (mounted.current && epoch === generation.current)
        if (!unauthorized(e))
          setError(e instanceof Error ? e.message : "לא ניתן לפתוח את הפנייה");
    } finally {
      if (epoch === generation.current) choosing.current = false;
    }
  }
  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!selected || busy) return;
    setBusy(true);
    setError("");
    try {
      const next = await advisorApi.respond(
        selected.ticket,
        action,
        text,
        amount === "" ? null : Number(amount),
        action === "answer" && selected.ticket.category === "Participation"
          ? effectiveFrom
          : undefined,
      );
      setSelected(next);
      setText("");
      setAmount("");
      setEffectiveFrom(today());
      setTickets(await advisorApi.tickets());
    } catch (err) {
      if (!unauthorized(err))
        setError(err instanceof Error ? err.message : "השמירה נכשלה");
      if (err instanceof ApiError && err.status === 409) {
        // Preserve the draft and version until the representative explicitly refreshes.
        setError("הפנייה עודכנה. יש לרענן ולבדוק את התשובה לפני שליחה חוזרת.");
      }
    } finally {
      setBusy(false);
    }
  }
  const open = tickets.filter(
    (t) => t.status === "Open" || t.status === "ClarificationRequested",
  );
  async function signOut() {
    setBusy(true);
    generation.current++;
    choosing.current = false;
    try {
      await advisorApi.representativeSignOut();
      clearSession();
      setCode("");
    } catch (e) {
      if (!unauthorized(e))
        setError(e instanceof Error ? e.message : "היציאה נכשלה");
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <PageTitle
        eyebrow="גורם אנושי, אחריות מלאה"
        title="מרכז הנציגים"
        description="מענה עסקי מתועד שמחזיר את העובד בדיוק לשלב שבו המתין. ללא גישה אוטומטית לנתוני השכר."
        action={
          <button
            className="btn secondary"
            disabled={busy}
            onClick={() => refresh(false)}
          >
            <RefreshCw size={16} />
            רענון
          </button>
        }
      />
      <div className="representative-demo">
        <ShieldCheck size={20} />
        <div>
          <strong>תפקיד נציג הדגמה · סביבת האקתון פיקטיבית</strong>
          <p>
            זהו אימות Cookie נפרד לעובדים ולנציגים, לא הזדהות ארגונית. אין להזין
            מידע אמיתי. ניתן לכבות כניסות הדגמה ולהחליף באימות ארגוני.
          </p>
        </div>
      </div>
      {error && (
        <p className="chat-error" role="alert">
          {error}
        </p>
      )}
      {!ready ? (
        <form onSubmit={login} className="panel representative-login">
          <LockKeyhole size={32} />
          <h2>כניסה לתפקיד נציג הדגמה</h2>
          <p>
            הרשאת נציג אינה מוענקת על ידי הודעה לצ'אט. היא נבדקת בשרת וב־Cookie
            חתום נפרד.
          </p>
          <label>
            קוד נציג (אם הוגדר בסביבה)
            <input
              type="password"
              autoComplete="off"
              value={code}
              onChange={(e) => setCode(e.target.value)}
            />
          </label>
          <button className="btn primary" disabled={busy || loading}>
            {busy ? "מתחברים…" : "כניסת נציג הדגמה"}
          </button>
        </form>
      ) : (
        <>
          <button
            className="btn secondary role-signout"
            disabled={busy}
            onClick={() => void signOut()}
          >
            יציאה מתפקיד נציג
          </button>
          <div className="representative-stats">
            <div className="panel">
              <Inbox size={22} />
              <strong>{open.length}</strong>
              <span>פניות פתוחות או ממתינות להבהרה</span>
            </div>
            <div className="panel">
              <CheckCircle2 size={22} />
              <strong>
                {tickets.filter((t) => t.status === "Answered").length}
              </strong>
              <span>פניות שנענו ונשמרו</span>
            </div>
          </div>
          <div className="representative-layout">
            <section className="panel ticket-queue">
              <div className="section-title">
                <h2>תור פניות</h2>
                <Tag>{tickets.length} פניות</Tag>
              </div>
              {tickets.length === 0 ? (
                <div className="state-box">
                  <Inbox size={30} />
                  <strong>אין פניות עדיין</strong>
                  <p>פנייה מהיועץ תופיע כאן לאחר שהעובד יבקש אישור אנושי.</p>
                </div>
              ) : (
                tickets.map((ticket) => (
                  <button
                    key={ticket.id}
                    className={`ticket-row ${selected?.ticket.id === ticket.id ? "active" : ""}`}
                    onClick={() => choose(ticket)}
                    disabled={busy}
                    aria-pressed={selected?.ticket.id === ticket.id}
                  >
                    <div>
                      <strong>#{ticket.id.slice(0, 8)}</strong>
                      <Tag
                        tone={
                          ticket.status === "Answered" ? "green" : "neutral"
                        }
                      >
                        {statusLabel(ticket.status)}
                      </Tag>
                    </div>
                    <p>
                      {ticket.category === "Participation"
                        ? "אישור השתתפות ברכב"
                        : "הבהרה עסקית"}
                    </p>
                    <small>
                      {ticket.vehicleId ?? "ללא רכב נבחר"} · קבוצה{" "}
                      {ticket.group}
                    </small>
                    <time dateTime={ticket.createdAt}>
                      {localTime(ticket.createdAt)}
                    </time>
                  </button>
                ))
              )}
            </section>
            <section className="panel ticket-details">
              {!selected ? (
                <div className="state-box">
                  <MessageSquare size={35} />
                  <strong>בוחרים פנייה וממשיכים משם</strong>
                  <p>
                    ההקשר המוצג כאן אינו כולל שכר, נקודות זיכוי או היסטוריית
                    השיחה הפרטית.
                  </p>
                </div>
              ) : (
                <>
                  <div className="section-title">
                    <h2>פנייה #{selected.ticket.id.slice(0, 8)}</h2>
                    <Tag>{statusLabel(selected.ticket.status)}</Tag>
                  </div>
                  <p className="ticket-question">{selected.ticket.question}</p>
                  <dl className="detail-list">
                    <div>
                      <dt>קטגוריה</dt>
                      <dd>
                        {selected.ticket.category === "Participation"
                          ? "השתתפות ברכב"
                          : "הבהרה עסקית"}
                      </dd>
                    </div>
                    <div>
                      <dt>עיסוק</dt>
                      <dd>{selected.ticket.occupation}</dd>
                    </div>
                    <div>
                      <dt>קבוצה / רכב</dt>
                      <dd>
                        {selected.ticket.group} /{" "}
                        {selected.ticket.vehicleId ?? "טרם נבחר"}
                      </dd>
                    </div>
                    <div>
                      <dt>שלב היעד</dt>
                      <dd>
                        {selected.ticket.target === "current"
                          ? "השתתפות במסלול הנוכחי"
                          : "אישור החלופה"}
                      </dd>
                    </div>
                    <div>
                      <dt>נוצרה</dt>
                      <dd>{localTime(selected.ticket.createdAt)}</dd>
                    </div>
                  </dl>
                  <section className="representative-responses">
                    <h3>תשובות והבהרות</h3>
                    {selected.responses.length === 0 ? (
                      <p>טרם נשלחה תשובה.</p>
                    ) : (
                      selected.responses.map((response) => (
                        <article key={response.id}>
                          <strong>
                            {response.actorRole === "employee"
                              ? "הבהרת עובד ששיתף במפורש"
                              : "תשובת נציג"}{" "}
                            · {localTime(response.createdAt)}
                          </strong>
                          <p>{response.text}</p>
                          {response.amount !== null && (
                            <p>השתתפות בסיסית מאושרת: {response.amount} ₪</p>
                          )}
                          {response.effectiveFrom && (
                            <p>בתוקף מ־{response.effectiveFrom}</p>
                          )}
                        </article>
                      ))
                    )}
                  </section>
                  {["Open", "ClarificationRequested"].includes(
                    selected.ticket.status,
                  ) && (
                    <form onSubmit={submit} className="representative-answer">
                      <label>
                        פעולה
                        <select
                          aria-label="פעולת נציג"
                          value={action}
                          onChange={(e) => setAction(e.target.value)}
                        >
                          <option value="answer">
                            מענה והמשך אוטומטי בשיחת העובד
                          </option>
                          <option value="clarification">
                            בקשת הבהרה מהעובד
                          </option>
                          <option value="close">סגירה ללא אישור</option>
                        </select>
                      </label>
                      {action === "answer" &&
                        selected.ticket.category === "Participation" && (
                          <label>
                            השתתפות עובד בסיסית מאושרת (₪)
                            <input
                              aria-label="סכום השתתפות מאושר"
                              type="number"
                              dir="ltr"
                              min={0}
                              max={1000000}
                              step="0.01"
                              required
                              value={amount}
                              onChange={(e) => setAmount(e.target.value)}
                            />
                            <small>
                              ללא חלק הפער המשפחתי; המנוע מוסיף אותו לפי הכללים.
                            </small>
                          </label>
                        )}
                      {action === "answer" &&
                        selected.ticket.category === "Participation" && (
                          <label>
                            בתוקף מתאריך
                            <input
                              aria-label="תאריך תחולת האישור"
                              type="date"
                              dir="ltr"
                              required
                              min="1900-01-01"
                              max={today()}
                              value={effectiveFrom}
                              onChange={(e) => setEffectiveFrom(e.target.value)}
                            />
                            <small>
                              אומדן חודשי בלבד, ללא חישוב הפרשים רטרואקטיביים.
                              אישור עתידי לא יוחל על החישוב הנוכחי.
                            </small>
                          </label>
                        )}
                      <label>
                        תשובת נציג
                        <textarea
                          aria-label="תשובת נציג"
                          rows={4}
                          required
                          maxLength={2000}
                          value={text}
                          onChange={(e) => setText(e.target.value)}
                        />
                      </label>
                      <button className="btn primary" disabled={busy}>
                        {busy ? "שומרים וממשיכים…" : "שמירת תשובה"}
                      </button>
                    </form>
                  )}
                  <details className="audit-trail">
                    <summary>
                      יומן פעולות מתועד ({selected.audit.length})
                    </summary>
                    {selected.audit.map((event) => (
                      <p key={event.id}>
                        {localTime(event.createdAt)} ·{" "}
                        {event.actorRole === "employee" ? "עובד" : "נציג"} ·{" "}
                        {event.action}
                      </p>
                    ))}
                  </details>
                </>
              )}
            </section>
          </div>
        </>
      )}
    </>
  );
}
