import { useEffect, useState } from "react";
import { BarChart3, RefreshCw } from "lucide-react";
import { qualityApi, type QualityView } from "../enterprise-api";
import { useWorkspace } from "../workspace";
import {
  useEmployeeAccess,
  SignedOutNotice,
} from "../components/EmployeeAccess";
import { PageTitle, Loading, ErrorState, Tag } from "../components/ui";
import { localTime } from "../advisor-api";
export function QualityDashboardPage() {
  const { employee } = useWorkspace();
  const access = useEmployeeAccess(employee.id);
  const [data, setData] = useState<QualityView | null>(null),
    [loading, setLoading] = useState(false),
    [error, setError] = useState(""),
    [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (!access.ready) return;
    let active = true;
    setLoading(true);
    setError("");
    setData(null);
    qualityApi()
      .then((result) => {
        if (active) setData(result);
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
  }, [access.ready, employee.id, attempt]);
  const metric = (label: string, value: number | string, note: string) => (
    <div className="panel quality-metric" key={label}>
      <span>{label}</span>
      <strong dir="auto">{value}</strong>
      <small>{note}</small>
    </div>
  );
  return (
    <>
      <PageTitle
        eyebrow="ראיות לפני הבטחות"
        title="איכות ואמינות AI"
        description="מדידות שימוש פרטיות, בדיקות כלים מתוסרטות ומודל חי — כל סוג ראיה מוצג בנפרד."
        action={
          <button
            className="btn secondary"
            disabled={loading || !access.ready}
            onClick={() => setAttempt((value) => value + 1)}
          >
            <RefreshCw size={16} />
            רענון מדידות
          </button>
        }
      />
      {access.signedOut ? (
        <SignedOutNotice enter={access.retry} />
      ) : access.error ? (
        <ErrorState message={access.error} retry={access.retry} />
      ) : access.loading || loading ? (
        <Loading />
      ) : error ? (
        <ErrorState
          message={error}
          retry={() => setAttempt((value) => value + 1)}
        />
      ) : (
        data && (
          <>
            <section className="panel quality-scope">
              <BarChart3 size={24} />
              <div>
                <strong>היסטוריית החשבון הנוכחי · עד 400 הודעות אחרונות</strong>
                <p>
                  אין כאן תוכן שיחות או נתוני שכר. אפס אירועים הוא נתון שנמדד;
                  איכות שלא נבדקה מסומנת כחסרה.
                </p>
              </div>
            </section>
            <div className="quality-metrics">
              {metric(
                "תשובות עוזר שנשמרו",
                data.runtime.assistantTurns,
                "כולל פתיחת שיחה; לא מדד להבנת שפה",
              )}
              {metric(
                "השוואות שהופקו",
                data.runtime.comparisonsProduced,
                "בתשובות העוזר ובאמצעות שירותי החישוב",
              )}
              {metric(
                "תוצאות כלי שהושלמו",
                `${data.runtime.completedToolCalls} / ${data.runtime.toolCalls}`,
                "השלמת כלי אינה אישור פעולה עסקית",
              )}
              {metric(
                "תוצאות כלי שנכשלו",
                data.runtime.failedToolCalls,
                "אירועים שנצפו בחלון המדידה",
              )}
              {metric(
                "כלים שהמתינו לקלט",
                data.runtime.needsInputToolCalls,
                "בקשת מידע אינה כישלון חישוב",
              )}
              {metric(
                "תשובות חלופיות לאחר כשל ספק",
                data.runtime.fallbackTurns,
                "fallback מסומן; לא תשובת מודל",
              )}
              {metric(
                "ניסיונות ספק שנצפו",
                data.runtime.providerAttempts,
                `${data.runtime.providerResponses} תוצאות ספק התקבלו; ללא ציון איכות סמנטית`,
              )}
              {metric(
                "חציון זמן ניסיון ספק",
                data.runtime.medianProviderLatencyMs === null
                  ? "לא נמדד"
                  : `${data.runtime.medianProviderLatencyMs} ms`,
                "רק כשקיימים ניסיונות מתועדים",
              )}
            </div>
            <section className="panel quality-evaluation">
              <h2>בדיקות כלים ו-groundedness מתוסרטות</h2>
              <Tag>בחירת הכלים מתוסרטת · אין קריאת מודל</Tag>
              {data.evaluation.scripted ? (
                <>
                  <p>
                    ריצה <bdi>{data.evaluation.runId}</bdi>
                    {data.evaluation.recordedAt &&
                      ` · ${localTime(data.evaluation.recordedAt)}`}
                  </p>
                  <p>
                    גרסת הקוד שנמדדה:{" "}
                    <bdi>{data.evaluation.codeCommit ?? "לא תועדה"}</bdi>.
                    המדידה מתייחסת לגרסה ולריצה המסומנות, לא לכל שינוי עתידי.
                  </p>
                  <dl className="quality-evaluation-list">
                    <div>
                      <dt>תרחישים שהושלמו</dt>
                      <dd>
                        {data.evaluation.scripted.completed} /{" "}
                        {data.evaluation.scripted.scenarios}
                      </dd>
                    </div>
                    <div>
                      <dt>השוואות שתאמו למנוע המאומת</dt>
                      <dd>
                        {data.evaluation.scripted.grounded} /{" "}
                        {data.evaluation.scripted.scenarios}
                      </dd>
                    </div>
                    <div>
                      <dt>שאלות השלמה מיותרות שנמדדו</dt>
                      <dd>{data.evaluation.scripted.unnecessaryQuestions}</dd>
                    </div>
                    <div>
                      <dt>שינויי זכאות או פניות לא צפויות שנמדדו</dt>
                      <dd>{data.evaluation.scripted.unsafeOutcomes}</dd>
                    </div>
                    <div>
                      <dt>מסעות נציג שנשמרו</dt>
                      <dd>
                        {data.evaluation.scripted.persistedHandoffs} /{" "}
                        {data.evaluation.scripted.handoffJourneys}
                      </dd>
                    </div>
                  </dl>
                  <p>
                    הנתונים קשורים לחתימות מקורות הפורטפוליו. הם אינם מוכיחים שהמודל
                    יבחר נכון כלים בעברית חדשה, ואינם מדדי הדיוק של המשתמש
                    הנוכחי.
                  </p>
                </>
              ) : (
                <p>
                  {data.evaluation.status === "stale_source"
                    ? "מקורות הנתונים השתנו; תוצאת הבדיקה הקודמת הוסרה עד מדידה חדשה."
                    : "לא פורסמה תוצאת בדיקה תקפה לבנייה זו. אין ציון משוער במקום מדידה."}
                </p>
              )}
            </section>
            <section className="panel quality-evaluation">
              <h2>איכות מודל חי</h2>
              <Tag tone="amber">לא נבדקה בריצה זו</Tag>
              <p>
                לא בוצעה הערכת עברית מול ספק אמיתי. תוצאות שימוש בספק, אם
                קיימות, אינן בדיקת groundedness עצמאית או הוכחה להכללה. אין
                ציון, עלות או זמן תגובה מומצאים.
              </p>
            </section>
            <section className="panel quality-evaluation">
              <h2>מה אפשר להסיק — ומה עדיין לא</h2>
              <ul>
                {data.limitations.map((limitation) => (
                  <li key={limitation}>{limitation}</li>
                ))}
              </ul>
            </section>
          </>
        )
      )}
    </>
  );
}
