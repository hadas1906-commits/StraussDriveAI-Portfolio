export function HandoffConfirmation({
  id,
  busy,
  confirm,
  kind = "create",
}: {
  id: string;
  busy: boolean;
  confirm: (id: string) => void;
  kind?: "create" | "cancel";
}) {
  return (
    <section className="facts-confirmation" aria-label="אישור פנייה לנציג">
      <strong>
        {kind === "cancel"
          ? "הפנייה עדיין פתוחה. הסגירה תתבצע רק לאחר אישורך, ללא אישור כספי."
          : "הפנייה עדיין לא נוצרה. יועברו עיסוק, קבוצה ורכב בלבד, ללא רכיבי השכר."}
      </strong>
      <button
        className="btn primary"
        disabled={busy}
        onClick={() => confirm(id)}
      >
        {kind === "cancel" ? "אישור סגירת הפנייה" : "אישור העברה לנציג"}
      </button>
    </section>
  );
}
