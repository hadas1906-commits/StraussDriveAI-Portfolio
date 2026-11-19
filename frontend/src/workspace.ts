import { createContext, useContext } from "react";
import type { Context, Employee, Vehicle } from "./api";

export interface Workspace {
  context: Context;
  employees: Employee[];
  employee: Employee;
  select: (id: string) => void;
  vehicles: Vehicle[];
  vehicleLoading: boolean;
  vehicleError: string;
  retryVehicles: () => void;
}

export const WorkspaceContext = createContext<Workspace | null>(null);

export function useWorkspace() {
  const data = useContext(WorkspaceContext);
  if (!data) throw new Error("Missing workspace");
  return data;
}
