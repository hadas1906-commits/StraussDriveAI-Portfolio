import { lazy, Suspense } from "react";
import { Link } from "react-router-dom";
import {
  UsersRound,
  ArrowLeft,
  ShieldCheck,
  Zap,
  Wallet,
  BadgeCheck,
  CarFront,
  Check,
} from "lucide-react";
import { money, initials } from "../lib/format";
import { useWorkspace } from "../workspace";
import {
  PageTitle,
  Tag,
  Loading,
  ErrorState,
  Empty,
  RuleNote,
} from "../components/ui";
import { CarArt } from "../components/CarArt";
import { VehicleCard } from "../components/VehicleCard";

const SalaryDonut = lazy(() =>
  import("../Charts").then((module) => ({ default: module.SalaryDonut })),
);

export function Dashboard() {
  const {
    employee,
    vehicles,
    vehicleLoading,
    vehicleError,
    retryVehicles,
    context,
  } = useWorkspace();
  const available = vehicles.filter((c) => c.eligibility.eligible);
  const components = [
    { name: "שכר בסיס", value: employee.baseSalary },
    { name: "עמלות", value: employee.commission },
    { name: "בונוס", value: employee.bonus },
  ].filter((c) => c.value > 0);
  return (
    <>
      <PageTitle
        eyebrow="מבט אישי על הטבות הרכב שלך"
        title={`שלום, ${employee.name.split(" ")[0]} 👋`}
        description="כל מה שצריך כדי לקבל החלטה טובה יותר. במקום אחד."
        action={
          <Link className="btn secondary" to="/employees">
            <UsersRound size={16} />
            הפרופיל שלי
          </Link>
        }
      />
      <section className="hero">
        <div className="hero-copy">
          <div className="hero-eyebrow">
            <span />
            הדרך שלך מתחילה כאן
          </div>
          <h2>
            רכב חדש.
            <br />
            החלטה חכמה יותר.
          </h2>
          <p>
            לפני שמחליפים מפתח, מבינים מה משתנה בנטו.
            <br />
            מגלים את האפשרויות, ומשווים עם כל המספרים.
          </p>
          <Link className="btn hero-button" to="/catalog">
            לגלות את הרכב הבא שלי
            <ArrowLeft size={18} />
          </Link>
          <div className="hero-proof">
            <ShieldCheck size={15} />
            נתוני מקור מאומתים<span>·</span>חישוב שקוף ומפורט
          </div>
        </div>
        <div className="hero-art" aria-hidden="true">
          <div className="orb orb-one" />
          <div className="orb orb-two" />
          <div className="road-line" />
          <CarArt electric hero />
          <span className="hero-float">
            <Zap size={17} />
            הבחירה מתחילה בנתונים
          </span>
        </div>
      </section>
      <section className="kpi-grid" aria-label="סקירת נתונים">
        <div className="kpi">
          <div className="kpi-icon teal">
            <Wallet size={21} />
          </div>
          <span>הנטו החודשי המשוער שלך</span>
          <strong className="large-money">
            {money(employee.currentSalary.displayNetSalary)}
          </strong>
          <small>לפי המסלול הנוכחי שלך</small>
        </div>
        <div className="kpi">
          <div className="kpi-icon blue">
            <BadgeCheck size={21} />
          </div>
          <span>קבוצת הזכאות שלך</span>
          <strong>קבוצה {employee.group}</strong>
          <small>תקציב חברה עד {money(employee.groupBudget)} לחודש</small>
        </div>
        <div className="kpi">
          <div className="kpi-icon lavender">
            <CarFront size={21} />
          </div>
          <span>רכבים במסגרת הזכאות</span>
          <strong>
            {vehicleLoading ? "—" : available.length}
            <em>אפשרויות עבורך</em>
          </strong>
          <small>מתוך {context.vehicleCount} רכבים בנתוני המקור</small>
        </div>
      </section>
      <div className="dashboard-columns">
        <section className="panel profile-panel">
          <div className="section-title">
            <h2>איפה אנחנו היום?</h2>
            <Tag tone="green">
              <Check size={12} />
              נתוני המקור
            </Tag>
          </div>
          <div className="profile-inline">
            <span className="avatar large">{initials(employee.name)}</span>
            <div>
              <strong>{employee.name}</strong>
              <p>{employee.occupation}</p>
            </div>
            <Link className="text-link" to="/employees">
              לפרופיל
              <ArrowLeft size={15} />
            </Link>
          </div>
          <div className="current-car">
            <div className="current-car-icon">
              <CarFront size={27} />
            </div>
            <div>
              <span>המסלול הנוכחי</span>
              <strong dir="auto">{employee.currentCar}</strong>
            </div>
          </div>
          <div className="profile-facts">
            <div>
              <span>השתתפות נוכחית</span>
              <strong>
                {money(employee.currentSalary.employeeNetParticipation)}
              </strong>
            </div>
            <div>
              <span>ילדים עד גיל {context.childMaxAge}</span>
              <strong>{employee.eligibleChildren}</strong>
            </div>
            <div>
              <span>נקודות זיכוי</span>
              <strong>{employee.creditPoints}</strong>
            </div>
          </div>
        </section>
        <section className="panel salary-panel">
          <div className="section-title">
            <h2>השכר שלך, במבט אחד</h2>
            <span className="small-muted">רכיבי ברוטו מהמקור</span>
          </div>
          <div className="salary-chart">
            <div
              className="donut-wrap"
              role="img"
              aria-label="תרשים רכיבי השכר ברוטו"
            >
              <Suspense
                fallback={<span className="sr-only">טוענים תרשים</span>}
              >
                <SalaryDonut components={components} />
              </Suspense>
              <div className="donut-center">
                <small>שכר בסיס</small>
                <strong>{money(employee.baseSalary)}</strong>
              </div>
            </div>
            <div className="chart-legend">
              {[
                { name: "שכר בסיס", value: employee.baseSalary },
                { name: "עמלות", value: employee.commission },
                { name: "בונוס", value: employee.bonus },
              ].map((c, i) => (
                <div key={c.name}>
                  <span>
                    <i
                      style={{
                        background: ["#168a81", "#82c6b8", "#d6eae5"][i],
                      }}
                    />
                    {c.name}
                  </span>
                  <strong>{money(c.value)}</strong>
                </div>
              ))}
            </div>
          </div>
        </section>
      </div>
      <section className="options-section">
        <div className="section-title">
          <div>
            <span className="eyebrow">הצעד הבא שלך</span>
            <h2>אפשרויות שכדאי להכיר</h2>
          </div>
          <Link className="text-link" to="/catalog">
            לכל הקטלוג
            <ArrowLeft size={17} />
          </Link>
        </div>
        {vehicleLoading ? (
          <Loading label="בודקים את האפשרויות שלך…" />
        ) : vehicleError ? (
          <ErrorState message={vehicleError} retry={retryVehicles} />
        ) : available.length ? (
          <div className="vehicle-grid dashboard-vehicles">
            {available.slice(0, 3).map((car) => (
              <VehicleCard key={car.id} car={car} compact />
            ))}
          </div>
        ) : (
          <Empty title="לא נמצאו רכבים בזכאות">
            אפשר לבדוק את נתוני העובד או לצפות בקטלוג המלא.
          </Empty>
        )}
      </section>
      <RuleNote />
    </>
  );
}
