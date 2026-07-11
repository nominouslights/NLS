import type { ServiceType, StatusKind } from "./theme";

export interface Trip {
  id: string;
  date: string;
  win: string;
  svc: ServiceType;
  from: string;
  to: string;
  stops: string[];
  km: number;
  driver: string | null;
  vehicle: string;
  cap: string;
  status: string;
  sk: StatusKind;
  client: string;
  po: string;
  open: boolean;
}

export interface Driver {
  id: number;
  name: string;
  src: string;
  duty: string;
  dk: StatusKind;
  hos: string;
  hk: StatusKind;
  lic: string;
  lk: StatusKind;
  dvir: string;
  clr: string[];
  phone: string;
  trips: number;
}

export interface FleetVehicle {
  id: number;
  unit: string;
  model: string;
  seats: number;
  status: string;
  sk: StatusKind;
  dvir: string;
  dk: StatusKind;
  insp: string;
  ik: StatusKind;
  plate: string;
  vin: string;
  licReq: string;
  periodic: boolean;
}

export interface ClientContact {
  name: string;
  role: string;
}

export interface Client {
  id: number;
  name: string;
  svc: ServiceType;
  tag: string;
  renew: string;
  rk: StatusKind;
  term: string;
  rate: string;
  po: string;
  gst: string;
  contacts: [string, string][];
  notes: string;
}

export interface Invoice {
  id: string;
  client: string;
  po: string;
  amt: string;
  code: string;
  status: string;
  sk: StatusKind;
  qbo: string;
  age: string;
}

export interface Rider {
  id: number;
  name: string;
  home: string;
  prog: "NIHB" | "Community";
  last: string;
  phone: string;
  pc: string;
  voucher: string;
  escort: string;
  noshow: string;
  needs: string;
}

export interface Incident {
  id: string;
  sev: string;
  sk: StatusKind;
  status: string;
  date: string;
  driver: string;
  vehicle: string;
  trip: string;
  summary: string;
}
