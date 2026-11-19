import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import {
  LoaderCircle,
  AlertCircle,
  RotateCcw,
  Compass,
  ShieldCheck,
  CarFront,
} from "lucide-react";
import { useWorkspace } from "../workspace";

export function Loading({
  label = "טוענים את הנתונים שלך…",
}: {
  label?: string;
}) {
  return (
    <div className="state-box" role="status">
      <LoaderCircle className="spin" size={28} />
      <strong>{label}</strong>
      <span>המידע מגיע ממנוע החישוב ונתוני הפורטפוליו</span>
    </div>
  );
}

export function ErrorState({
  message,
  retry,
}: {
  message: string;
  retry: () => void;
}) {
  return (
    <div className="state-box error" role="alert">
      <AlertCircle size={30} />
      <strong>משהו לא התחבר</strong>
      <p>{message}</p>
      <button className="btn secondary" onClick={retry}>
        <RotateCcw size={16} />
        ניסיון נוסף
      </button>
    </div>
  );
}

export function Empty({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <div className="state-box empty">
      <Compass size={36} />
      <h3>{title}</h3>
      <p>{children}</p>
    </div>
  );
}

export function Tag({
  children,
  tone = "neutral",
}: {
  children: ReactNode;
  tone?: "neutral" | "green" | "amber";
}) {
  return <span className={`tag ${tone}`}>{children}</span>;
}

export function PageTitle({
  eyebrow,
  title,
  description,
  action,
}: {
  eyebrow: string;
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <div className="page-heading">
      <div>
        <div className="eyebrow">{eyebrow}</div>
        <h1 tabIndex={-1}>{title}</h1>
        <p>{description}</p>
      </div>
      {action}
    </div>
  );
}

export function RuleNote() {
  const { context } = useWorkspace();
  return (
    <div className="rule-note">
      <ShieldCheck size={18} />
      <span>
        אומדן לפי כללי הפורטפוליו ולוחות מס {context.payrollTaxYear}. נתוני הדגמה,
        לא תלוש שכר. עיגול לשקל רק בתצוגה.
      </span>
    </div>
  );
}

export function Brand() {
  return (
    <Link to="/" className="brand" title="לעמוד הבית">
      <span className="brand-symbol">
        <CarFront size={24} />
      </span>
      <span dir="ltr">
        DriveAI<span className="brand-light"> Portfolio</span>
        <small>נתונים פיקטיביים · ללא AI חי</small>
      </span>
    </Link>
  );
}
