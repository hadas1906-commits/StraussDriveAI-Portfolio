import { ScenarioLabPage } from "./pages/ScenarioLab";
import { QualityDashboardPage } from "./pages/QualityDashboard";
import { useState, useEffect, useRef } from "react";
import { Link, NavLink, Route, Routes, useLocation } from "react-router-dom";
import {
  LayoutDashboard,
  UsersRound,
  CarFront,
  ArrowLeftRight,
  SlidersHorizontal,
  Menu,
  X,
  ChevronDown,
  ShieldCheck,
  ArrowLeft,
  Bot,
  Headset,
  FlaskConical,
  BarChart3,
} from "lucide-react";
import { api, type Context, type Employee, type Vehicle } from "./api";
import { initials } from "./lib/format";
import { type Workspace, WorkspaceContext } from "./workspace";
import {
  Brand,
  Loading,
  ErrorState,
  Empty,
  Tag,
  PageTitle,
} from "./components/ui";
import { Dashboard } from "./pages/Dashboard";
import { Employees } from "./pages/Employees";
import { Catalog } from "./pages/Catalog";
import { ComparisonRoute } from "./pages/Comparison";
import { AdvisorPage, AssistantLauncher } from "./pages/Advisor";
import { RepresentativePage } from "./pages/Representative";

const routes = [
  { path: "/lab", label: "מעבדת תרחישים", icon: FlaskConical },
  { path: "/quality", label: "איכות ואמינות AI", icon: BarChart3 },
  { path: "/advisor", label: "יועץ הרכב בשיחה", icon: Bot },
  { path: "/representative", label: "מרכז הנציגים", icon: Headset },
  { path: "/", label: "הסקירה שלי", icon: LayoutDashboard },
  { path: "/employees", label: "בחירת עובד", icon: UsersRound },
  { path: "/catalog", label: "קטלוג הרכבים", icon: CarFront },
  { path: "/compare", label: "השוואת נטו", icon: ArrowLeftRight },
  { path: "/simulator", label: "סימולטור שכר", icon: SlidersHorizontal },
];

export default function App() {
  const [bootstrap, setBootstrap] = useState<{
    context: Context;
    employees: Employee[];
  } | null>(null);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const [selected, setSelected] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    setError("");
    setBootstrap(null);
    Promise.all([
      api.context(controller.signal),
      api.employees(controller.signal),
    ])
      .then(([context, employees]) => {
        if (controller.signal.aborted) return;
        setBootstrap({ context, employees });
        let saved = "";
        try {
          saved = localStorage.getItem("strauss-employee") ?? "";
        } catch {
          /* Storage is optional. */
        }
        setSelected(
          employees.some((e) => e.id === saved)
            ? saved
            : (employees[0]?.id ?? ""),
        );
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError(e.message);
      });
    return () => controller.abort();
  }, [attempt]);
  if (error)
    return (
      <div className="startup">
        <Brand />
        <ErrorState message={error} retry={() => setAttempt((a) => a + 1)} />
      </div>
    );
  if (!bootstrap)
    return (
      <div className="startup">
        <Brand />
        <Loading />
      </div>
    );
  const employee = bootstrap.employees.find((e) => e.id === selected);
  if (!employee)
    return (
      <div className="startup">
        <Empty title="אין עובדים להצגה">
          לא נמצאו עובדים בנתוני המקור. יש לבדוק את ייבוא הנתונים בשרת.
        </Empty>
      </div>
    );
  return (
    <WorkspaceShell
      context={bootstrap.context}
      employees={bootstrap.employees}
      employee={employee}
      select={(id) => {
        setSelected(id);
        try {
          localStorage.setItem("strauss-employee", id);
        } catch {
          /* Storage is optional. */
        }
      }}
    />
  );
}

function WorkspaceShell({
  context,
  employees,
  employee,
  select,
}: Pick<Workspace, "context" | "employees" | "employee" | "select">) {
  const [vehiclesState, setVehicles] = useState<{
    id: string;
    items: Vehicle[];
    error: string;
    loading: boolean;
  }>({ id: "", items: [], error: "", loading: true });
  const [attempt, setAttempt] = useState(0);
  const [menu, setMenu] = useState(false);
  const location = useLocation();
  const main = useRef<HTMLElement>(null);
  const drawer = useRef<HTMLElement>(null);
  const [mobile, setMobile] = useState(
    () => window.matchMedia("(max-width:760px)").matches,
  );
  useEffect(() => {
    const media = window.matchMedia("(max-width:760px)");
    const update = () => {
      setMobile(media.matches);
      if (!media.matches) setMenu(false);
    };
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);
  useEffect(() => {
    if (!menu || !mobile) return;
    drawer.current?.querySelector<HTMLAnchorElement>("a")?.focus();
    const keyboard = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setMenu(false);
        requestAnimationFrame(() =>
          document.querySelector<HTMLButtonElement>(".mobile-menu")?.focus(),
        );
      }
      if (event.key === "Tab") {
        const items = Array.from(
          drawer.current?.querySelectorAll<HTMLElement>(
            "a[href],button:not([disabled])",
          ) ?? [],
        ).filter((el) => el.offsetParent !== null);
        const first = items[0],
          last = items.at(-1);
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first?.focus();
        }
      }
    };
    document.addEventListener("keydown", keyboard);
    return () => document.removeEventListener("keydown", keyboard);
  }, [menu, mobile]);
  useEffect(() => {
    const controller = new AbortController();
    setVehicles({ id: employee.id, items: [], error: "", loading: true });
    api
      .vehicles(employee.id, controller.signal)
      .then((items) => {
        if (!controller.signal.aborted)
          setVehicles({ id: employee.id, items, error: "", loading: false });
      })
      .catch((e) => {
        if (!controller.signal.aborted)
          setVehicles({
            id: employee.id,
            items: [],
            error: e.message,
            loading: false,
          });
      });
    return () => controller.abort();
  }, [employee.id, attempt]);
  useEffect(() => {
    setMenu(false);
    document.title = `${routes.find((r) => r.path === location.pathname)?.label ?? "DriveAI Portfolio"} | DriveAI Portfolio`;
    main.current?.querySelector("h1")?.focus();
  }, [location.pathname]);
  useEffect(() => {
    if (!menu) main.current?.querySelector("h1")?.focus();
  }, [location.pathname, menu]);
  const current = vehiclesState.id === employee.id;
  const data: Workspace = {
    context,
    employees,
    employee,
    select,
    vehicles: current ? vehiclesState.items : [],
    vehicleLoading: !current || vehiclesState.loading,
    vehicleError: current ? vehiclesState.error : "",
    retryVehicles: () => setAttempt((a) => a + 1),
  };
  return (
    <WorkspaceContext.Provider value={data}>
      <a className="skip-link" href="#main-content">
        דילוג לתוכן הראשי
      </a>
      {menu && (
        <button
          className="nav-backdrop"
          aria-label="סגירת ניווט"
          onClick={() => setMenu(false)}
        />
      )}
      <aside
        ref={drawer}
        className={`sidebar ${menu ? "open" : ""}`}
        inert={mobile && !menu}
        role={mobile && menu ? "dialog" : undefined}
        aria-modal={mobile && menu ? true : undefined}
        aria-label="תפריט ניווט"
      >
        <button
          className="icon-button drawer-close"
          aria-label="סגירת תפריט"
          onClick={() => {
            setMenu(false);
            requestAnimationFrame(() =>
              document
                .querySelector<HTMLButtonElement>(".mobile-menu")
                ?.focus(),
            );
          }}
        >
          <X size={20} />
        </button>
        <Brand />
        <div className="workspace-label">סביבת העובד</div>
        <nav aria-label="ניווט ראשי">
          {routes.map(({ path, label, icon: Icon }) => (
            <NavLink key={path} to={path} end={path === "/"}>
              <Icon size={20} />
              {label}
              <span className="nav-dot" />
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <div className="sidebar-help">
            <ShieldCheck size={23} />
            <strong>הבחירה שלך, עם כל הנתונים.</strong>
            <p>השוואה שקופה המבוססת על כללי הפורטפוליו ומאגר נתונים פיקטיביים.</p>
          </div>
          <div className="demo-label">
            <span />
            LOCAL PORTFOLIO<span className="demo-year">DEMO</span>
          </div>
        </div>
      </aside>
      <div className="app-body" inert={mobile && menu}>
        <header className="topbar">
          <div className="topbar-title">
            <button
              className="icon-button mobile-menu"
              aria-label="פתיחת ניווט"
              aria-expanded={menu}
              onClick={() => setMenu(!menu)}
            >
              {menu ? <X size={22} /> : <Menu size={22} />}
            </button>
            <span>מרחב ההטבות שלי</span>
            <span className="topbar-divider" />
            <Tag>נתוני הדגמה</Tag>
          </div>
          <div className="topbar-actions">
            <div className="server-status">
              <span />
              נתוני מקור נטענו
            </div>
            <label className="employee-switch">
              <span className="avatar">{initials(employee.name)}</span>
              <select
                aria-label="בחירת עובד פעיל"
                value={employee.id}
                onChange={(e) => select(e.target.value)}
              >
                {employees.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
              <ChevronDown size={15} />
            </label>
          </div>
        </header>
        <main id="main-content" ref={main}>
          <Routes>
            <Route
              path="/lab"
              element={<ScenarioLabPage key={employee.id} />}
            />
            <Route
              path="/quality"
              element={<QualityDashboardPage key={employee.id} />}
            />
            <Route path="/advisor" element={<AdvisorPage />} />
            <Route path="/representative" element={<RepresentativePage />} />
            <Route path="/" element={<Dashboard />} />
            <Route path="/employees" element={<Employees />} />
            <Route path="/catalog" element={<Catalog />} />
            <Route path="/compare" element={<ComparisonRoute />} />
            <Route path="/simulator" element={<ComparisonRoute simulate />} />
            <Route path="*" element={<NotFound />} />
          </Routes>
          {location.pathname !== "/advisor" &&
            location.pathname !== "/representative" && <AssistantLauncher />}
          <footer className="page-footer">
            <span dir="ltr">DriveAI Portfolio · Fictional local demo</span>
            <span>
              כל הנתונים והחישובים מגיעים מהשרת · שנת מס{" "}
              {context.payrollTaxYear}
            </span>
          </footer>
        </main>
      </div>
    </WorkspaceContext.Provider>
  );
}

function NotFound() {
  return (
    <>
      <PageTitle
        eyebrow="DriveAI Portfolio"
        title="העמוד הזה יצא לדרך אחרת"
        description="אפשר לחזור לסקירה האישית ולהמשיך משם."
      />
      <Link to="/" className="btn primary">
        בחזרה לסקירה שלי
        <ArrowLeft size={16} />
      </Link>
    </>
  );
}
