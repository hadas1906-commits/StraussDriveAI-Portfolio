import type { DecisionEvidence as Evidence } from "../api";
import { money } from "../lib/format";
export function DecisionEvidence({ evidence }: { evidence?: Evidence | null }) {
  if (!evidence) return null;
  return (
    <details className="panel decision-evidence">
      <summary>למה התקבלה התוצאה? כללים, מקורות והנחות</summary>
      <p>
        <strong>תוצאה מחושבת לפי הקלט · אומדן שכר</strong> ·{" "}
        {evidence.inputKind === "imported_sample"
          ? "קלט מדוגמת המקור"
          : "כולל קלט לתרחיש או לשיחה"}
      </p>
      <p>
        גרסת כללים <bdi>{evidence.ruleVersion}</bdi> · לוחות שכר{" "}
        {evidence.payrollTaxYear}. זכאות נקבעת בשירות המאומת, לא על ידי מודל
        השפה.
      </p>
      <h3>הכללים שהופעלו</h3>
      <ul>
        {evidence.rules.map((rule) => (
          <li key={rule}>{rule}</li>
        ))}
      </ul>
      <h3>הנחות ואי-ודאות</h3>
      <ul>
        {evidence.assumptions.map((assumption) => (
          <li key={assumption}>{assumption}</li>
        ))}
      </ul>
      <h3>עקיבות למאגר הרכבים</h3>
      {evidence.vehicles.length === 0 && (
        <p>שני המסלולים ללא רכב; לא נשלף שווי שימוש.</p>
      )}
      {evidence.vehicles.map((vehicle) => (
        <p key={vehicle.scenario}>
          {vehicle.scenario === "current" ? "מצב נוכחי" : "חלופה"}:{" "}
          <a href={vehicle.url} target="_blank" rel="noreferrer">
            {vehicle.carId} · רשומת מקור
          </a>{" "}
          · שווי שימוש {money(vehicle.valueOfUse)}
          <br />
          <bdi>
            {vehicle.key.taxYear} / {vehicle.key.registrationYear} /{" "}
            {vehicle.key.vehicleTypeCode} / {vehicle.key.manufacturerCode} /{" "}
            {vehicle.key.modelCode}
          </bdi>
        </p>
      ))}
      <p>
        <a href="/api/advisor/sources/rules" target="_blank" rel="noreferrer">
          פתיחת כללי המקור
        </a>
      </p>
      <details>
        <summary>חתימות קובצי המקור</summary>
        <ul>
          {evidence.sources.map((source) => (
            <li key={source.sha256}>
              {source.name}
              <code dir="ltr">{source.sha256}</code>
            </li>
          ))}
        </ul>
      </details>
    </details>
  );
}
