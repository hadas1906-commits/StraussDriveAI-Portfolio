/** Match catalog names, manufacturer/model combinations and exact catalog IDs. */
const manufacturerAliases: Record<string, string> = {};
const words = (value: string) =>
  value
    .normalize("NFKC")
    .toUpperCase()
    .replace(/(\p{L})(\p{N})/gu, "$1 $2")
    .replace(/(\p{N})(\p{L})/gu, "$1 $2")
    .replace(/[^\p{L}\p{N}]+/gu, " ")
    .trim()
    .split(/\s+/)
    .filter(Boolean);

export function matchesVehicle(
  vehicle: { id: string; name: string; manufacturer: string },
  query: string,
): boolean {
  const q = query.trim();
  if (!q) return true;
  if (/^DEMO[\s-]*\d+$/i.test(q))
    return vehicle.id.toUpperCase() === q.toUpperCase();
  const tokens = words(q);
  if (!tokens.length) return false;
  const haystack = words(
    `${vehicle.manufacturer} ${manufacturerAliases[vehicle.manufacturer] ?? ""} ${vehicle.name}`,
  );
  return tokens.every((token) => haystack.includes(token));
}
