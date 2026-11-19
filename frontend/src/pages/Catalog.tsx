import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { FileCheck2, Search, RotateCcw } from "lucide-react";
import { useWorkspace } from "../workspace";
import {
  PageTitle,
  Tag,
  RuleNote,
  Loading,
  ErrorState,
  Empty,
} from "../components/ui";
import { VehicleCard } from "../components/VehicleCard";
import { matchesVehicle } from "../lib/vehicle-search";

export function Catalog() {
  const { vehicles, vehicleLoading, vehicleError, retryVehicles, context } =
    useWorkspace();
  const [params, setParams] = useSearchParams();
  const [group, setGroup] = useState("all");
  const [drive, setDrive] = useState("all");
  const [eligible, setEligible] = useState("all");
  const q = params.get("q") ?? "";
  const filtered = vehicles.filter(
    (v) =>
      (group === "all" || v.group === group) &&
      (drive === "all" || v.drive === drive) &&
      (eligible === "all" || v.eligibility.eligible) &&
      matchesVehicle(v, q),
  );
  return (
    <>
      <PageTitle
        eyebrow="הרכב הנכון הוא בחירה אישית"
        title="מוצאים את הדרך שלך"
        description="כל הרכבים, העלויות והזכאות שלך — בלי סימני שאלה."
        action={<Tag>{context.vehicleCount} רכבים בקטלוג</Tag>}
      />
      <div className="catalog-note">
        <FileCheck2 size={18} />
        <span>
          מציגים את כל {context.vehicleCount} הרכבים שבקובץ הפיקטיבי, כולל המסלול
          המשפחתי. תיאור הפורטפוליו מציין 22; בקובץ הנתונים זמינים{" "}
          {context.vehicleCount}.
        </span>
      </div>
      <div className="filter-bar">
        <label className="search-field">
          <Search size={18} />
          <input
            type="search"
            dir="auto"
            aria-label="חיפוש רכב"
            value={q}
            onChange={(e) =>
              setParams(e.target.value ? { q: e.target.value } : {}, {
                replace: true,
              })
            }
            placeholder="דגם, יצרן או מזהה רכב…"
          />
        </label>
        <label>
          <span>קבוצה</span>
          <select
            aria-label="סינון לפי קבוצה"
            value={group}
            onChange={(e) => setGroup(e.target.value)}
          >
            <option value="all">כל הקבוצות</option>
            {[...new Set(vehicles.map((v) => v.group))].map((g) => (
              <option key={g}>{g}</option>
            ))}
          </select>
        </label>
        <label>
          <span>הנעה</span>
          <select
            aria-label="סינון לפי הנעה"
            value={drive}
            onChange={(e) => setDrive(e.target.value)}
          >
            <option value="all">כל סוגי ההנעה</option>
            {[...new Set(vehicles.map((v) => v.drive))].map((d) => (
              <option key={d}>{d}</option>
            ))}
          </select>
        </label>
        <label>
          <span>זכאות</span>
          <select
            aria-label="סינון לפי זכאות"
            value={eligible}
            onChange={(e) => setEligible(e.target.value)}
          >
            <option value="all">כל הרכבים</option>
            <option value="eligible">במסגרת הזכאות שלי</option>
          </select>
        </label>
      </div>
      <div className="results-heading">
        <span role="status" aria-live="polite" aria-atomic="true">
          {vehicleLoading
            ? "טוענים רכבים…"
            : vehicleError
              ? "הקטלוג אינו זמין"
              : `${filtered.length} רכבים נמצאו`}
        </span>
        <button
          className="text-link"
          onClick={() => {
            setParams({});
            setGroup("all");
            setDrive("all");
            setEligible("all");
          }}
        >
          <RotateCcw size={13} />
          איפוס סינון
        </button>
      </div>
      {vehicleLoading ? (
        <Loading label="טוענים קטלוג וזכאות…" />
      ) : vehicleError ? (
        <ErrorState message={vehicleError} retry={retryVehicles} />
      ) : filtered.length ? (
        <div className="vehicle-grid">
          {filtered.map((v) => (
            <VehicleCard key={v.id} car={v} />
          ))}
        </div>
      ) : (
        <Empty title="המסלול הנכון עוד מחכה לך">
          לא נמצאו רכבים התואמים לסינון. נסו לשנות את החיפוש או לאפס את המסננים.
        </Empty>
      )}
      <RuleNote />
    </>
  );
}
