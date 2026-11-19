import { DecisionEvidence } from "../components/DecisionEvidence";
import {
  useState,
  useEffect,
  useRef,
  lazy,
  Suspense,
  type FormEvent,
} from "react";
import { useSearchParams } from "react-router-dom";
import {
  SlidersHorizontal,
  Wallet,
  LockKeyhole,
  LoaderCircle,
  Gauge,
  RotateCcw,
  ShieldCheck,
  ArrowLeftRight,
  TrendingUp,
  CircleHelp,
  Zap,
} from "lucide-react";
import { api, type Comparison, type Scenario } from "../api";
import { money, precise, signed, initials } from "../lib/format";
import { useWorkspace } from "../workspace";
import {
  PageTitle,
  Tag,
  RuleNote,
  Loading,
  ErrorState,
} from "../components/ui";
import { CarArt } from "../components/CarArt";

const WaterfallChart = lazy(() =>
  import("../Charts").then((module) => ({ default: module.WaterfallChart })),
);

export function ComparisonRoute({ simulate = false }: { simulate?: boolean }) {
  const { employee } = useWorkspace();
  return (
    <ComparisonPage key={`${employee.id}-${simulate}`} simulate={simulate} />
  );
}

function ComparisonPage({ simulate }: { simulate: boolean }) {
  const { employee, vehicles, vehicleLoading, vehicleError, retryVehicles } =
    useWorkspace();
  const [params] = useSearchParams();
  const [selection, setSelection] = useState(params.get("car") ?? "");
  const [result, setResult] = useState<Comparison | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const original = () => ({
    baseSalary: String(employee.baseSalary),
    commission: String(employee.commission),
    bonus: String(employee.bonus),
    pensionRate: precise(employee.pensionRate * 100).replaceAll(",", ""),
    studyFundRate: precise(employee.studyFundRate * 100).replaceAll(",", ""),
    creditPoints: String(employee.creditPoints),
  });
  const [inputs, setInputs] = useState(original);
  const choices = vehicles.filter((v) => v.eligibility.eligible);
  const selectedCar = vehicles.find((v) => v.id === selection);
  const validSelection =
    selection === "waiver"
      ? employee.waiver.eligible
      : !!selectedCar?.eligibility.eligible;
  useEffect(() => {
    if (simulate || !selection || vehicleLoading) return;
    const abort = new AbortController();
    setResult(null);
    setError("");
    setBusy(false);
    if (!validSelection) {
      setError(
        "החלופה שנבחרה אינה קיימת או שאינה בזכאות שלך. בחרו חלופה אחרת.",
      );
      return () => abort.abort();
    }
    setBusy(true);
    api
      .compare(
        employee.id,
        selection === "waiver" ? null : selection,
        abort.signal,
      )
      .then((response) => {
        if (!abort.signal.aborted) setResult(response);
      })
      .catch((e) => {
        if (!abort.signal.aborted) setError(e.message);
      })
      .finally(() => {
        if (!abort.signal.aborted) setBusy(false);
      });
    return () => abort.abort();
  }, [selection, employee.id, validSelection, vehicleLoading, simulate, retry]);
  useEffect(() => () => controller.current?.abort(), []);
  function edit() {
    controller.current?.abort();
    setResult(null);
    setBusy(false);
    setError("");
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!validSelection) return;
    controller.current?.abort();
    const abort = new AbortController();
    controller.current = abort;
    setBusy(true);
    setError("");
    setResult(null);
    try {
      const values = {
        baseSalary: Number(inputs.baseSalary),
        commission: Number(inputs.commission),
        bonus: Number(inputs.bonus),
        pensionPercent: Number(inputs.pensionRate),
        studyFundPercent: Number(inputs.studyFundRate),
        creditPoints: Number(inputs.creditPoints),
      };
      if (Object.values(values).some((v) => !Number.isFinite(v)))
        throw new Error("יש למלא ערך מספרי תקין בכל שדה.");
      const response = await api.simulate(
        employee.id,
        selection === "waiver" ? null : selection,
        values,
        abort.signal,
      );
      if (!abort.signal.aborted) setResult(response);
    } catch (e) {
      if (!abort.signal.aborted)
        setError(e instanceof Error ? e.message : "החישוב נכשל");
    } finally {
      if (!abort.signal.aborted) setBusy(false);
    }
  }
  return (
    <>
      <PageTitle
        eyebrow={
          simulate ? "מרחב לבדיקת אפשרויות" : "פחות ניחושים. יותר בהירות."
        }
        title={simulate ? "מה יקרה אם…?" : "אותה משכורת. בחירה אחרת."}
        description={
          simulate
            ? "משנים רכיבי שכר, בוחרים חלופה ומקבלים חישוב חדש מהשרת."
            : "השוואה שקופה בין המסלול הנוכחי לרכב הבא שלך — עד השקל."
        }
      />
      <div className="comparison-layout">
        <form className="panel scenario-form" onSubmit={submit}>
          <div className="section-title">
            <h2>{simulate ? "בניית התרחיש שלך" : "בוחרים לאן ממשיכים"}</h2>
            <SlidersHorizontal size={18} />
          </div>
          <div className="mini-profile">
            <span className="avatar">{initials(employee.name)}</span>
            <div>
              <strong>{employee.name}</strong>
              <span>
                {employee.occupation} · קבוצה {employee.group}
              </span>
            </div>
          </div>
          {simulate && (
            <>
              <div className="form-section-label">רכיבי שכר חודשיים</div>
              <div className="input-grid">
                {(
                  [
                    { key: "baseSalary", label: "שכר בסיס (₪)" },
                    { key: "commission", label: "עמלות (₪)" },
                    { key: "bonus", label: "בונוס (₪)" },
                    { key: "pensionRate", label: "פנסיה עובד (%)" },
                    { key: "studyFundRate", label: "קרן השתלמות (%)" },
                    { key: "creditPoints", label: "נקודות זיכוי" },
                  ] as const
                ).map((f) => (
                  <label key={f.key}>
                    {f.label}
                    <input
                      dir="ltr"
                      type="number"
                      required
                      min="0"
                      max={f.key.endsWith("Rate") ? 100 : undefined}
                      step="0.01"
                      value={inputs[f.key]}
                      onChange={(e) => {
                        edit();
                        setInputs({ ...inputs, [f.key]: e.target.value });
                      }}
                    />
                  </label>
                ))}
              </div>
              <p className="form-help">
                אותם רכיבי שכר יוחלו על המסלול הנוכחי והחלופה. הזכאות, הילדים
                וערכי הרכב נשארים לפי המקור.
              </p>
            </>
          )}
          <label className="alternative-label">
            {simulate ? "החלופה לבדיקה" : "איזו חלופה מעניין אותך לבדוק?"}
            <select
              aria-label="בחירת חלופה"
              value={selection}
              required
              onChange={(e) => {
                edit();
                setSelection(e.target.value);
              }}
            >
              <option value="">בחירת רכב או מסלול…</option>
              {choices.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.name} · {v.drive} · קבוצה {v.group}
                </option>
              ))}
              {employee.waiver.eligible && (
                <option value="waiver">ויתור על רכב — אחזקת רכב</option>
              )}
            </select>
          </label>
          {selectedCar && (
            <div className="selected-vehicle">
              <CarArt electric={selectedCar.drive === "חשמלי"} />
              <strong dir="auto">{selectedCar.name}</strong>
              <span>
                {money(selectedCar.participation)} השתתפות בסיס ·{" "}
                {money(selectedCar.valueOfUse)} שווי שימוש
              </span>
              {selectedCar.eligibility.familyEmployeeShare > 0 && (
                <span>
                  תוספת חלק העובד בפער:{" "}
                  {money(selectedCar.eligibility.familyEmployeeShare)}
                </span>
              )}
            </div>
          )}
          {selection === "waiver" && (
            <div className="waiver-info">
              <Wallet size={24} />
              <strong>אחזקת רכב: {money(employee.maintenanceAllowance)}</strong>
              <span>חייבת במס, ללא הפרשות סוציאליות</span>
            </div>
          )}
          {!employee.waiver.eligible && (
            <p className="form-help">
              <LockKeyhole size={14} />
              העיסוק מחייב רכב חברה — ויתור אינו אפשרי.
            </p>
          )}
          {simulate && (
            <>
              <button
                className="btn primary"
                type="submit"
                disabled={!validSelection || busy}
              >
                {busy ? (
                  <LoaderCircle className="spin" size={17} />
                ) : (
                  <Gauge size={17} />
                )}
                חישוב התרחיש
              </button>
              <button
                type="button"
                className="text-link reset-inputs"
                onClick={() => {
                  edit();
                  setInputs(original());
                }}
              >
                <RotateCcw size={14} />
                חזרה לנתוני המקור
              </button>
            </>
          )}
          <div className="secure-note">
            <ShieldCheck size={16} />
            החישוב מתבצע במנוע השכר הקיים בשרת
          </div>
        </form>
        <section className="comparison-results" aria-busy={busy}>
          {vehicleLoading ? (
            <Loading />
          ) : vehicleError ? (
            <ErrorState message={vehicleError} retry={retryVehicles} />
          ) : busy ? (
            <Loading label="מחשבים את ההשפעה על הנטו…" />
          ) : error ? (
            <ErrorState
              message={error}
              retry={() => {
                if (simulate)
                  setError("בחרו חלופה תקינה ולחצו על חישוב התרחיש.");
                else setRetry((n) => n + 1);
              }}
            />
          ) : result ? (
            <ComparisonResult result={result} simulated={simulate} />
          ) : (
            <div className="panel comparison-empty">
              <div className="empty-illustration">
                <ArrowLeftRight size={42} />
                <span />
                <span />
              </div>
              <h2>הבחירה הבאה שלך מתחילה כאן</h2>
              <p>
                {simulate
                  ? "מלאו את רכיבי השכר, בחרו חלופה ולחצו על חישוב התרחיש."
                  : "בחרו רכב או מסלול מהרשימה כדי לראות בדיוק מה משתנה בנטו."}
              </p>
              <div>
                <ShieldCheck size={16} />
                ללא הערכות מקומיות. כל סכום מחושב בשרת.
              </div>
            </div>
          )}
        </section>
      </div>
      <RuleNote />
    </>
  );
}

export function ComparisonResult({
  result,
  simulated,
}: {
  result: Comparison;
  simulated: boolean;
}) {
  const { context } = useWorkspace();
  const positive = result.displayNetDifference >= 0;
  const rows: { label: string; get: (s: Scenario) => number; note?: string }[] =
    [
      ...(result.current.benefit && result.alternative.benefit
        ? [
            {
              label: "תקציב רכב לפי זכאות",
              get: (s: Scenario) => s.benefit!.budget,
              note: "אינו תוספת לשכר",
            },
            {
              label: "עלות הרכב החודשית לחברה",
              get: (s: Scenario) => s.benefit!.monthlyCost,
              note: "אינה ניכוי מהנטו",
            },
            {
              label: "חלק המעסיק בפער המסלול המשפחתי",
              get: (s: Scenario) => s.benefit!.familyEmployerShare,
            },
          ]
        : []),
      { label: "שכר ברוטו (בסיס, עמלות ובונוס)", get: (s) => s.grossSalary },
      { label: "אחזקת רכב חייבת", get: (s) => s.maintenanceAllowance },
      {
        label: "החזר חשמל ברוטו מגולם",
        get: (s) => s.salary.electricReimbursementGross,
      },
      { label: "סך שכר במזומן", get: (s) => s.salary.grossCashSalary },
      {
        label: "שווי שימוש ברכב",
        get: (s) => s.valueOfUse,
        note: "רכיב חייב במס; אינו משולם במזומן",
      },
      { label: "הכנסה חייבת במס ובביטוח", get: (s) => s.salary.taxableIncome },
      { label: "מס הכנסה אחרי זיכויים", get: (s) => s.salary.incomeTax },
      { label: "ביטוח לאומי", get: (s) => s.salary.nationalInsurance },
      { label: "ביטוח בריאות", get: (s) => s.salary.healthInsurance },
      { label: "פנסיה עובד", get: (s) => s.salary.employeePension },
      { label: "קרן השתלמות עובד", get: (s) => s.salary.studyFund },
      {
        label: "השתתפות רכב כוללת",
        get: (s) => s.salary.employeeNetParticipation,
      },
      {
        label: "מתוכה: חלק העובד במסלול משפחתי",
        get: (s) => s.familyEmployeeShare,
      },
    ];
  return (
    <>
      <div
        className={`delta-card ${positive ? "positive" : "negative"}`}
        aria-live="polite"
        aria-atomic="true"
      >
        <div>
          <span className="eyebrow">
            {simulated ? "תוצאת התרחיש" : "השינוי החודשי המשוער שלך"}
          </span>
          <strong data-testid="monthly-delta" dir="ltr">
            {signed(result.displayNetDifference)}
          </strong>
          <p>
            {result.displayNetDifference === 0
              ? "ללא שינוי בנטו החודשי"
              : positive
                ? "יותר נטו בכל חודש"
                : "פחות נטו בכל חודש"}{" "}
            בהשוואה למסלול הנוכחי
          </p>
        </div>
        <div className="annual-delta">
          <TrendingUp size={22} />
          <span>בקצב שנתי משוער</span>
          <strong dir="ltr">{signed(result.displayAnnualDifference)}</strong>
          <small>12 חודשים באותם תנאים</small>
        </div>
      </div>
      <div className="net-cards">
        <div className="panel">
          <span>המסלול הנוכחי{simulated ? " · בשכר שהוזן" : ""}</span>
          <strong>{money(result.current.salary.displayNetSalary)}</strong>
          <small dir="auto">{result.current.name}</small>
          {result.current.benefit && (
            <p className="benefit-explanation">
              {result.current.benefit.eligibilityReason}
            </p>
          )}
        </div>
        <div className="panel alternative-net">
          <Tag tone="green">החלופה שלך</Tag>
          <strong>{money(result.alternative.salary.displayNetSalary)}</strong>
          <small dir="auto">{result.alternative.name}</small>
          {result.alternative.benefit && (
            <p className="benefit-explanation">
              {result.alternative.benefit.eligibilityReason}
            </p>
          )}
          {result.alternative.salary.electricReimbursementGross > 0 && (
            <p className="electric-net-note">
              <Zap size={12} />
              החזר חשמל מגולם: {money(context.electricNetBonus)} נטו
            </p>
          )}
        </div>
      </div>
      <DecisionEvidence evidence={result.evidence} />
      <section className="panel waterfall-panel">
        <div className="section-title">
          <div>
            <h2>מה מרכיב את השינוי?</h2>
            <p>כל צעד בדרך מהנטו הנוכחי לחלופה</p>
          </div>
          <ArrowLeftRight size={20} />
        </div>
        <div
          className="waterfall"
          role="img"
          aria-label="תרשים מפל השינוי בנטו; פירוט מלא בטבלה בהמשך"
          dir="ltr"
        >
          <Suspense fallback={<span className="sr-only">טוענים תרשים</span>}>
            <div className="waterfall-inner">
              <WaterfallChart points={result.waterfall} />
            </div>
          </Suspense>
        </div>
        <div className="waterfall-legend">
          <span>
            <i className="dot navy" />
            נטו
          </span>
          <span>
            <i className="dot teal" />
            תוספת לנטו
          </span>
          <span>
            <i className="dot coral" />
            הפחתה מהנטו
          </span>
        </div>
      </section>
      <section className="panel breakdown">
        <div className="section-title">
          <h2>כל המספרים, בשקיפות מלאה</h2>
          <Tag>₪ לחודש</Tag>
        </div>
        <div className="table-scroll">
          <table>
            <caption className="sr-only">
              פירוט המסלול הנוכחי מול החלופה
            </caption>
            <thead>
              <tr>
                <th scope="col">רכיב שכר</th>
                <th scope="col">היום</th>
                <th scope="col">בחלופה</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.label}>
                  <th scope="row">
                    {r.label}
                    {r.note && <small>{r.note}</small>}
                  </th>
                  <td>{money(r.get(result.current))}</td>
                  <td>{money(r.get(result.alternative))}</td>
                </tr>
              ))}
              <tr className="total-row">
                <th scope="row">נטו חודשי משוער</th>
                <td>{money(result.current.salary.displayNetSalary)}</td>
                <td>{money(result.alternative.salary.displayNetSalary)}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <details className="precision-note">
          <summary>
            <CircleHelp size={15} />
            איך מוצג ההפרש?
          </summary>
          <p>
            ההפרש המוצג הוא ההפרש בין סכומי הנטו המעוגלים, בהתאם לקובץ הפורטפוליו.
            ההפרש ללא עיגול: <bdi>{precise(result.netDifference)} ₪</bdi>.
            המספרים והתרשים מגיעים מהשרת; שווי שימוש אינו ניכוי מזומן נוסף.
          </p>
        </details>
      </section>
    </>
  );
}
