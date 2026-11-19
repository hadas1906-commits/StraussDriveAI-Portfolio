import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { matchesVehicle } from "./lib/vehicle-search";
import { money, signed } from "./lib/format";
import { ProviderStatus } from "./components/ProviderStatus";
import { ComparisonSummary } from "./components/ComparisonSummary";
const vehicle = { id: "DEMO-102", name: "PULSE", manufacturer: "Fictional Orion Studio" };
describe("original portfolio vehicle identities", () => {
  it.each(["PULSE", "pulse", "DEMO-102", "Orion PULSE"])("matches %s", q => expect(matchesVehicle(vehicle,q)).toBe(true));
  it.each(["DEMO-10", "DEMO-1020", "DEMO-999", "!!!"])("rejects %s", q => expect(matchesVehicle(vehicle,q)).toBe(false));
});
it("labels deterministic answers without claiming a live model", () => {
 render(<ProviderStatus message={{mode:"deterministic-demo"}}/>);
 expect(screen.getByText(/דמו דטרמיניסטי/)).toBeInTheDocument();
 expect(screen.getByText(/ללא קריאת AI/)).toBeInTheDocument();
 expect(screen.queryByText(/מודל חי התקבלה/)).not.toBeInTheDocument();
});
it("shows blocked alternatives as unavailable instead of invented zero salaries", () => {
 render(<ComparisonSummary summary={{tableOnly:false,rows:[{vehicleId:"DEMO-106",name:"HARBOR",baseline:false,status:"ineligible",explanation:"Fictional family eligibility blocked",monthlyNet:null,monthlyDifference:null,annualDifference:null}],recommendation:null}}/>);
 expect(screen.getByText("HARBOR")).toBeInTheDocument();expect(screen.getAllByText("לא זמין")).toHaveLength(3);
 expect(screen.getByText(/eligibility blocked/)).toBeInTheDocument();
});
it("formats fixture outcomes with an explicit positive sign",()=>{
 expect(money(10138)).toContain("10,138");expect(signed(193)).toMatch(/^\+/);
});
