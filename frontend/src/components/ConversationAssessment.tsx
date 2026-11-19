import type { Assessment } from "../advisor-api";
const fields: Record<string, string> = {
  occupation: "עיסוק",
  baseSalary: "שכר בסיס",
  commission: "עמלות",
  bonus: "בונוס",
  pensionPercent: "פנסיה",
  studyFundPercent: "קרן השתלמות",
  creditPoints: "נקודות זיכוי",
  eligibleChildren: "ילדים בזכאות",
  currentCar: "מסלול נוכחי",
  currentParticipation: "השתתפות נוכחית",
  verifiedGroup: "קבוצה מאומתת",
  alternativeParticipation: "השתתפות בחלופה",
};
const fieldLabel = (field: string) =>
  fields[field.charAt(0).toLowerCase() + field.slice(1)] ?? field;
const sources: Record<string, string> = {
  verified_import: "מקור הפורטפוליו",
  verified_profile_snapshot: "צילום פרופיל המקור",
  structured_employee_input: "קלט מפורש לתרחיש",
  employee_input: "קלט עובד",
  employee_confirmed: "שינוי שאושר",
  model_suggestion: "הצעת מודל",
  employee_claim: "טענה",
  representative_approval: "אישור נציג",
  server_context: "הקשר שרת",
  none: "טרם הוזן",
};
export function ConversationAssessment({
  assessment,
}: {
  assessment?: Assessment | null;
}) {
  if (!assessment) return null;
  return (
    <details className="conversation-assessment">
      <summary>
        מקורות הנתונים ואי-ודאות ·{" "}
        {{
          ready: "אפשר להמשיך",
          awaiting_confirmation: "שינוי ממתין לאישור",
          needs_input: "חסר מידע",
          conflict: "טענה דורשת בירור",
          waiting_for_representative: "ממתינים לנציג",
        }[assessment.status] ?? "בדיקת מצב"}
      </summary>
      <p>{assessment.nextStep}</p>
      {!!assessment.missingFields.length && (
        <p>
          נתון להשלמה: {assessment.missingFields.map(fieldLabel).join(", ")}
        </p>
      )}
      {assessment.conflicts.map((conflict) => (
        <p className="assessment-conflict" key={conflict}>
          {conflict}
        </p>
      ))}
      <ul>
        {assessment.inputs.map((input) => (
          <li key={input.field}>
            {fieldLabel(input.field)} · {sources[input.source] ?? "מקור מתועד"}{" "}
            ·{" "}
            {input.status === "usable"
              ? "זמין לחישוב; אינו אישור לשכר אמיתי"
              : input.status === "awaiting_confirmation"
                ? "טרם אושר לשימוש"
                : input.status === "missing"
                  ? "חסר"
                  : "נדרשת בדיקה מול המקור"}
          </li>
        ))}
      </ul>
    </details>
  );
}
