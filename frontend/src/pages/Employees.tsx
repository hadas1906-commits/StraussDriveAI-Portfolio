import { ArrowLeft, Check, ShieldCheck } from "lucide-react";
import { money, precise, initials } from "../lib/format";
import { useWorkspace } from "../workspace";
import { PageTitle, Tag, RuleNote } from "../components/ui";

export function Employees() {
  const { employees, employee, select, context } = useWorkspace();
  return (
    <>
      <PageTitle
        eyebrow="אנשים לפני מספרים"
        title="הפרופיל שמאחורי הבחירה"
        description={`${context.employeeCount} עובדים לדוגמה מהקובץ הפיקטיבי. בחרו פרופיל כדי לראות את האפשרויות המדויקות עבורו.`}
      />
      <div className="employees-grid">
        {employees.map((e) => (
          <article
            className={`panel employee-card ${employee.id === e.id ? "selected" : ""}`}
            key={e.id}
          >
            <div className="section-title">
              <span className="avatar large">{initials(e.name)}</span>
              <Tag tone={employee.id === e.id ? "green" : "neutral"}>
                {employee.id === e.id ? (
                  <>
                    <Check size={12} />
                    הפרופיל הפעיל
                  </>
                ) : (
                  e.id
                )}
              </Tag>
            </div>
            <h2>{e.name}</h2>
            <p className="employee-occupation">
              {e.occupation} · קבוצה {e.group}
            </p>
            <div className="employee-net">
              <span>נטו חודשי משוער</span>
              <strong>{money(e.currentSalary.displayNetSalary)}</strong>
            </div>
            <dl className="detail-list">
              <div>
                <dt>שכר בסיס</dt>
                <dd>{money(e.baseSalary)}</dd>
              </div>
              <div>
                <dt>עמלות / בונוס</dt>
                <dd>
                  {money(e.commission)} / {money(e.bonus)}
                </dd>
              </div>
              <div>
                <dt>פנסיה / קרן השתלמות</dt>
                <dd>
                  {precise(e.pensionRate * 100)}% /{" "}
                  {precise(e.studyFundRate * 100)}%
                </dd>
              </div>
              <div>
                <dt>נקודות זיכוי</dt>
                <dd>{e.creditPoints}</dd>
              </div>
              <div>
                <dt>ילדים עד גיל {context.childMaxAge}</dt>
                <dd>{e.eligibleChildren}</dd>
              </div>
              <div>
                <dt>תקציב קבוצה</dt>
                <dd>{money(e.groupBudget)}</dd>
              </div>
              <div className="full-detail">
                <dt>המסלול הנוכחי</dt>
                <dd dir="auto">{e.currentCar}</dd>
              </div>
            </dl>
            <p className="eligibility">
              <ShieldCheck size={16} />
              {e.waiver.eligible
                ? "ניתן לוותר על רכב בהתאם לכללי העיסוק"
                : "רכב חברה חובה במסגרת העיסוק"}
            </p>
            <button
              className={`btn ${e.id === employee.id ? "secondary" : "primary"}`}
              onClick={() => select(e.id)}
              disabled={e.id === employee.id}
            >
              {e.id === employee.id ? "זה הפרופיל שלי" : "בחירת הפרופיל"}
              <ArrowLeft size={16} />
            </button>
          </article>
        ))}
      </div>
      <RuleNote />
    </>
  );
}
