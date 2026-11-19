import { useEffect, useState } from "react";
import { FlaskConical, Plus } from "lucide-react";
import { type SalaryInputs } from "../api";
import { labApi, type LabRequest, type LabResult } from "../enterprise-api";
import { useWorkspace } from "../workspace";
import {
  useEmployeeAccess,
  SignedOutNotice,
} from "../components/EmployeeAccess";
import { PageTitle, Loading, ErrorState, Tag } from "../components/ui";
import { ComparisonResult } from "./Comparison";
import { money, signed } from "../lib/format";
const fields: { key: keyof SalaryInputs; label: string; max: number }[] = [
  { key: "baseSalary", label: "שכר בסיס", max: 100000000 },
  { key: "commission", label: "עמלות", max: 100000000 },
  { key: "bonus", label: "בונוס", max: 100000000 },
  { key: "pensionPercent", label: "פנסיה עובד (%)", max: 100 },
  { key: "studyFundPercent", label: "קרן השתלמות עובד (%)", max: 100 },
  { key: "creditPoints", label: "נקודות זיכוי", max: 100 },
];
export function ScenarioLabPage() {
  const { employee, vehicles, vehicleLoading } = useWorkspace();
  const access = useEmployeeAccess(employee.id);
  const api = labApi(employee.id);
  const [inputs, setInputs] = useState<SalaryInputs>({
    baseSalary: employee.baseSalary,
    commission: employee.commission,
    bonus: employee.bonus,
    pensionPercent: employee.pensionRate * 100,
    studyFundPercent: employee.studyFundRate * 100,
    creditPoints: employee.creditPoints,
  });
  const [name, setName] = useState("תרחיש 1"),
    [car, setCar] = useState(employee.currentCarId ?? ""),
    [drafts, setDrafts] = useState<LabRequest[]>([]),
    [results, setResults] = useState<LabResult[]>([]),
    [saved, setSaved] = useState<LabResult[]>([]),
    [selected, setSelected] = useState<LabResult | null>(null),
    [loading, setLoading] = useState(true),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  useEffect(() => {
    if (!access.ready) {
      setLoading(false);
      return;
    }
    let active = true;
    setLoading(true);
    setError("");
    api
      .list()
      .then((list) => {
        if (active) setSaved(list);
      })
      .catch((e) => {
        if (active) setError(e.message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [access.ready, employee.id]);
  async function add(e: React.FormEvent) {
    e.preventDefault();
    if (busy || drafts.length >= 4) return;
    setBusy(true);
    setError("");
    const next = [
      ...drafts,
      {
        name: name.trim(),
        alternativeCarId: car || null,
        inputs: { ...inputs },
      },
    ];
    try {
      const previews = await api.preview(next);
      setDrafts(next);
      setResults(previews);
      setName(`תרחיש ${next.length + 1}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "הבדיקה נכשלה");
    } finally {
      setBusy(false);
    }
  }
  async function save(result: LabResult) {
    setBusy(true);
    setError("");
    try {
      await api.save(result.request);
      setSaved(await api.list());
    } catch (e) {
      setError(e instanceof Error ? e.message : "השמירה נכשלה");
    } finally {
      setBusy(false);
    }
  }
  async function remove(result: LabResult) {
    if (!result.id) return;
    setBusy(true);
    setError("");
    try {
      await api.remove(result.id);
      setSaved(await api.list());
      if (selected?.id === result.id) setSelected(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "המחיקה נכשלה");
    } finally {
      setBusy(false);
    }
  }
  function table(items: LabResult[], persisted: boolean) {
    return (
      <div className="table-scroll">
        <table className="scenario-table">
          <caption>
            {persisted
              ? "תרחישים שמורים לחשבון הנוכחי"
              : "השוואת תרחישים מול הפרופיל המיובא"}
          </caption>
          <thead>
            <tr>
              <th scope="col">תרחיש</th>
              <th scope="col">נטו המקור</th>
              <th scope="col">נטו בתרחיש</th>
              <th scope="col">הפרש חודשי</th>
              <th scope="col">מצב ופעולות</th>
            </tr>
          </thead>
          <tbody>
            {items.map((result, index) => (
              <tr key={result.id ?? index}>
                <th scope="row">{result.name}</th>
                <td>
                  {result.comparison
                    ? money(result.comparison.current.salary.displayNetSalary)
                    : "—"}
                </td>
                <td>
                  {result.comparison
                    ? money(
                        result.comparison.alternative.salary.displayNetSalary,
                      )
                    : "לא חושב"}
                </td>
                <td>
                  {result.comparison
                    ? signed(result.comparison.displayNetDifference)
                    : "—"}
                </td>
                <td>
                  <p>
                    {result.comparison
                      ? "אומדן מחושב · תרחיש בלבד"
                      : result.nextStep}
                  </p>
                  {result.comparison && (
                    <button
                      className="text-link"
                      disabled={busy}
                      onClick={() => setSelected(result)}
                      aria-label={`פירוט ${result.name}`}
                    >
                      הסבר ופירוט
                    </button>
                  )}
                  {!persisted && result.comparison && (
                    <button
                      className="text-link"
                      disabled={busy || saved.length >= 12}
                      onClick={() => void save(result)}
                      aria-label={`שמירת ${result.name}`}
                    >
                      שמירה פרטית
                    </button>
                  )}
                  {persisted && (
                    <button
                      className="text-link"
                      disabled={busy}
                      onClick={() => void remove(result)}
                      aria-label={`מחיקת ${result.name}`}
                    >
                      מחיקה
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }
  return (
    <>
      <PageTitle
        eyebrow="בודקים החלטות, שומרים על המקור"
        title="מעבדת תרחישים"
        description="השוו כמה השערות שכר ורכב מול המצב המיובא המקורי. הזכאות והפרופיל אינם משתנים."
        action={<Tag>תרחישים היפותטיים בלבד</Tag>}
      />
      {access.signedOut ? (
        <SignedOutNotice enter={access.retry} />
      ) : access.error ? (
        <ErrorState message={access.error} retry={access.retry} />
      ) : access.loading || loading || vehicleLoading ? (
        <Loading />
      ) : (
        <>
          <section className="panel lab-baseline">
            <FlaskConical size={24} />
            <div>
              <strong>
                בסיס קבוע: {employee.name} · קבוצה {employee.group}
              </strong>
              <p>
                נטו מקור {money(employee.currentSalary.displayNetSalary)}.
                עיסוק, ילדים, מסלול נוכחי והשתתפות מקור נשארים מאומתים לדוגמת
                הפורטפוליו. שינויים בטופס חלים רק על החלופה.
              </p>
            </div>
          </section>
          {error && (
            <p className="chat-error" role="alert">
              {error}{" "}
              <button
                className="text-link"
                type="button"
                onClick={access.retry}
              >
                רענון המעבדה
              </button>
            </p>
          )}
          <form className="panel scenario-form" onSubmit={add}>
            <fieldset disabled={busy || drafts.length >= 4}>
              <legend>תרחיש חדש · עד ארבע חלופות בבדיקה</legend>
              <label>
                שם התרחיש
                <input
                  required
                  maxLength={80}
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </label>
              <label>
                רכב בתרחיש
                <select
                  aria-label="רכב בתרחיש"
                  value={car}
                  onChange={(e) => setCar(e.target.value)}
                >
                  <option value="">ויתור על רכב</option>
                  {vehicles.map((vehicle) => (
                    <option key={vehicle.id} value={vehicle.id}>
                      {vehicle.name} ·{" "}
                      {vehicle.eligibility.eligible ? "בזכאות" : "מחוץ לזכאות"}
                    </option>
                  ))}
                </select>
              </label>
              <div className="scenario-input-grid">
                {fields.map((field) => (
                  <label key={field.key}>
                    {field.label}
                    <input
                      type="number"
                      dir="ltr"
                      required
                      min={0}
                      max={field.max}
                      step="any"
                      aria-label={`תרחיש: ${field.label}`}
                      value={
                        Number.isNaN(inputs[field.key]) ? "" : inputs[field.key]
                      }
                      onChange={(e) =>
                        setInputs({
                          ...inputs,
                          [field.key]:
                            e.target.value === ""
                              ? Number.NaN
                              : Number(e.target.value),
                        })
                      }
                    />
                  </label>
                ))}
              </div>
              <button className="btn primary" type="submit">
                <Plus size={16} />
                {busy ? "בודקים תרחישים…" : "הוספת תרחיש להשוואה"}
              </button>
            </fieldset>
          </form>
          {!!results.length && (
            <section
              className="panel scenario-results"
              aria-label="תוצאות תרחישים"
            >
              <h2>החלטה מול אותו בסיס</h2>
              {table(results, false)}
              <button
                className="btn secondary"
                disabled={busy}
                onClick={() => {
                  setDrafts([]);
                  setResults([]);
                  setSelected(null);
                }}
              >
                ניקוי ההשוואה
              </button>
            </section>
          )}
          <section className="panel scenario-results">
            <h2>התרחישים השמורים שלי ({saved.length}/12)</h2>
            <p>
              נשמרים בחשבון הפרטי בלבד. המקור לא משתנה והזכאות נבדקת שוב בכל
              טעינה.
            </p>
            {saved.length ? (
              table(saved, true)
            ) : (
              <p>עדיין אין תרחישים שמורים. הוסיפו חלופה והשוו לפני השמירה.</p>
            )}
          </section>
          {selected?.comparison && (
            <section className="lab-selected">
              <h2>פירוט התרחיש: {selected.name}</h2>
              <p>{selected.nextStep}</p>
              <ComparisonResult
                result={selected.comparison}
                simulated={false}
              />
            </section>
          )}
        </>
      )}
    </>
  );
}
