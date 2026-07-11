export function statusShort(status: string): string {
  if (status.startsWith("Empty")) return "Empty leg";
  return status.split("—")[0].trim();
}

export function initials(name: string): string {
  return name
    .replace(".", "")
    .split(" ")
    .map((x) => x[0])
    .join("");
}
