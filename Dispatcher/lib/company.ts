// Static Northern Link letterhead / carrier data for generated documents
// (e.g. the NL-WO-01 work order). Data-only — no logic, no rendering.

export interface CompanyInfo {
  name: string;
  legalName: string;
  address: string;
  phone: string;
  email: string;
  authorizedBy: string;
  nscNo: string; // blank until issued
  sfcNo: string; // Safety Fitness Certificate — blank until issued
}

export const COMPANY: CompanyInfo = {
  name: "Northern Link Shuttle & Cargo",
  legalName: "Northern Link Shuttle & Cargo (sole proprietorship — Emelio Campbell)",
  address: "Box 89, Leaf Rapids, Manitoba",
  phone: "(204) 441-7724",
  email: "emelio.campbell@northernlinkshuttleandcargo.com",
  authorizedBy: "Emelio Campbell, Owner/Operator",
  nscNo: "",
  sfcNo: "",
};
