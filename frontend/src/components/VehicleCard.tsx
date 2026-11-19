import { Link } from "react-router-dom";
import { Zap, Fuel, BadgeCheck, LockKeyhole, ArrowLeft } from "lucide-react";
import type { Vehicle } from "../api";
import { money } from "../lib/format";
import { CarArt } from "./CarArt";
import { Tag } from "./ui";

export function VehicleCard({
  car,
  compact = false,
}: {
  car: Vehicle;
  compact?: boolean;
}) {
  return (
    <article
      className={`vehicle-card ${!car.eligibility.eligible ? "ineligible" : ""}`}
    >
      <div className="vehicle-visual">
        <div className="vehicle-badges">
          <Tag>קבוצה {car.group}</Tag>
          <Tag tone={car.drive === "חשמלי" ? "green" : "neutral"}>
            {car.drive === "חשמלי" ? <Zap size={12} /> : <Fuel size={12} />}{" "}
            {car.drive}
          </Tag>
        </div>
        <CarArt electric={car.drive === "חשמלי"} />
        <span className="illustration-label">איור להמחשה</span>
      </div>
      <div className="vehicle-body">
        <div className="vehicle-title">
          <div>
            <span className="small-muted">
              {car.manufacturer} · {car.key.registrationYear}
            </span>
            <h3 dir="auto">{car.name}</h3>
          </div>
          <div className="cost">
            <strong>{money(car.monthlyCost)}</strong>
            <span>עלות חודשית לחברה</span>
          </div>
        </div>
        <div className="vehicle-stats">
          <div>
            <span>השתתפות עובד</span>
            <strong>{money(car.participation)}</strong>
          </div>
          <div>
            <span>שווי שימוש חייב</span>
            <strong>{money(car.valueOfUse)}</strong>
          </div>
        </div>
        {!compact && (
          <>
            <p
              className={`eligibility ${car.eligibility.eligible ? "allowed" : "denied"}`}
            >
              {car.eligibility.eligible ? (
                <BadgeCheck size={16} />
              ) : (
                <LockKeyhole size={15} />
              )}
              <span>{car.eligibility.reason}</span>
            </p>
            {car.eligibility.familyEmployeeShare > 0 && (
              <p className="family-note">
                תוספת חלק העובד בפער:{" "}
                {money(car.eligibility.familyEmployeeShare)}
              </p>
            )}
            <details className="source-key">
              <summary>זיהוי במאגר נתונים פיקטיביים</summary>
              <span dir="ltr">
                {car.key.taxYear} / {car.key.registrationYear} /{" "}
                {car.key.vehicleTypeCode} / {car.key.manufacturerCode} /{" "}
                {car.key.modelCode}
              </span>
            </details>
          </>
        )}
        {car.eligibility.eligible ? (
          <Link
            className="btn card-action"
            to={`/compare?car=${encodeURIComponent(car.id)}`}
          >
            בדיקת ההשפעה על הנטו
            <ArrowLeft size={16} />
          </Link>
        ) : (
          <button className="btn card-action" disabled>
            מחוץ לזכאות שלך
            <LockKeyhole size={14} />
          </button>
        )}
      </div>
    </article>
  );
}
