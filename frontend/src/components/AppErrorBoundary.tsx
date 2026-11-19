import { Component, type ReactNode } from "react";
import { AlertCircle, RotateCcw } from "lucide-react";

export class AppErrorBoundary extends Component<
  { children: ReactNode },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="startup">
        <div className="state-box error" role="alert">
          <AlertCircle size={32} />
          <h1>לא הצלחנו לטעון את העמוד</h1>
          <p>אפשר לנסות לטעון מחדש. לא יוצגו תוצאות חישוב חלקיות.</p>
          <button
            className="btn secondary"
            onClick={() => window.location.reload()}
          >
            <RotateCcw size={16} />
            טעינה מחדש
          </button>
        </div>
      </div>
    );
  }
}
