export const money = (n: number) =>
  new Intl.NumberFormat("he-IL", {
    style: "currency",
    currency: "ILS",
    maximumFractionDigits: 0,
    minimumFractionDigits: 0,
  }).format(n);

export const precise = (n: number) =>
  new Intl.NumberFormat("he-IL", { maximumFractionDigits: 4 }).format(n);

export const signed = (n: number) => `${n > 0 ? "+" : ""}${money(n)}`;

export const initials = (name: string) =>
  name
    .split(" ")
    .map((s) => s[0])
    .slice(0, 2)
    .join("");
