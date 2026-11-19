import { useEffect, useState } from "react";
import { employeeSession } from "../advisor-api";
export function useEmployeeAccess(profile: string) {
  const [attempt, setAttempt] = useState(0);
  const [state, setState] = useState({
    profile: "",
    ready: false,
    loading: true,
    error: "",
    signedOut: false,
  });
  useEffect(() => {
    let active = true;
    let signedOut = false;
    try {
      signedOut = sessionStorage.getItem("sd.employee.signedOut") === "true";
    } catch {}
    setState({
      profile,
      ready: false,
      loading: !signedOut,
      error: "",
      signedOut,
    });
    if (!signedOut)
      employeeSession(profile)
        .then(() => {
          if (active)
            setState({
              profile,
              ready: true,
              loading: false,
              error: "",
              signedOut: false,
            });
        })
        .catch((e) => {
          if (active)
            setState({
              profile,
              ready: false,
              loading: false,
              error: e instanceof Error ? e.message : "הכניסה נכשלה",
              signedOut: false,
            });
        });
    return () => {
      active = false;
    };
  }, [profile, attempt]);
  const retry = () => {
    try {
      sessionStorage.removeItem("sd.employee.signedOut");
    } catch {}
    setAttempt((value) => value + 1);
  };
  return {
    ...state,
    ready: state.profile === profile && state.ready,
    loading: state.profile !== profile || state.loading,
    retry,
  };
}
export function SignedOutNotice({ enter }: { enter: () => void }) {
  return (
    <section className="panel state-box">
      <strong>יצאת מהחשבון הפרטי</strong>
      <p>
        כניסה מחדש יוצרת חשבון הדגמה נפרד. נתונים קודמים אינם משוחזרים אוטומטית.
      </p>
      <button className="btn primary" onClick={enter}>
        כניסה לחשבון הדגמה חדש
      </button>
    </section>
  );
}
