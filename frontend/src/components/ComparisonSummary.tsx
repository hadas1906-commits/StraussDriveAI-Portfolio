import type { ComparisonSummary as Summary } from "../advisor-api";
import { money, signed } from "../lib/format";

export function ComparisonSummary({ summary }: { summary: Summary }) {
  return (
    <section
      className="comparison-summary"
      dir="rtl"
      aria-label="סיכום השוואת רכבים"
    >
      <table>
        <caption>השוואת נטו לפי מנוע החישוב</caption>
        <thead>
          <tr>
            <th scope="col">רכב</th>
            <th scope="col">נטו חודשי משוער</th>
            <th scope="col">שינוי לעומת היום</th>
            <th scope="col">שינוי שנתי משוער</th>
            <th scope="col">זכאות / מגבלות</th>
          </tr>
        </thead>
        <tbody>
          {summary.rows.map((row) => (
            <tr
              key={`${row.baseline ? "baseline" : "alternative"}-${row.vehicleId}`}
            >
              <th scope="row">
                {row.name}
                {row.baseline ? " (היום)" : ""}
              </th>
              <td data-label="נטו חודשי משוער">
                <bdi>
                  {row.monthlyNet == null ? "לא זמין" : money(row.monthlyNet)}
                </bdi>
              </td>
              <td data-label="שינוי לעומת היום">
                <bdi>
                  {row.monthlyDifference == null
                    ? "לא זמין"
                    : signed(row.monthlyDifference)}
                </bdi>
              </td>
              <td data-label="שינוי שנתי משוער">
                <bdi>
                  {row.annualDifference == null
                    ? "לא זמין"
                    : signed(row.annualDifference)}
                </bdi>
              </td>
              <td data-label="זכאות / מגבלות">{row.explanation}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {summary.recommendation && (
        <p className="comparison-recommendation">{summary.recommendation}</p>
      )}
    </section>
  );
}
