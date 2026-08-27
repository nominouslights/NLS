// NL-WO-01 work order — composes the two-page printable HTML from the section
// builders. Page 1 (§1–§6) is prefilled from data; page 2 (§7–§13) is blank for
// the repair shop — the authorization limit (§12) is emitted by that page, with
// a one-line pointer to it left at the foot of page 1.

import type { Vehicle } from "@/lib/api";
import type { Shop, WorkOrder } from "@/lib/types";
import type { CompanyInfo } from "@/lib/company";
import { WORK_ORDER_STYLES } from "./styles";
import {
  carrierBlock,
  footer,
  header,
  partsByCarrierBlock,
  reasonBlock,
  shopBlock,
  shopCompletionPage2,
  spendingLimitPointer,
  topStrip,
  vehicleBlock,
  workRequestedBlock,
} from "./sections";

export function workOrderHtml(
  wo: WorkOrder,
  vehicle: Vehicle,
  shop: Shop | null,
  company: CompanyInfo,
): string {
  return `
<style>${WORK_ORDER_STYLES}</style>
<div class="wo">
  <div class="sheet">
    ${header(company)}
    ${topStrip(wo)}
    ${carrierBlock(company)}
    ${shopBlock(shop)}
    ${vehicleBlock(vehicle)}
    ${reasonBlock(wo)}
    ${workRequestedBlock(wo)}
    ${partsByCarrierBlock()}
    ${spendingLimitPointer()}
    ${footer()}
    ${shopCompletionPage2(wo, vehicle)}
  </div>
</div>`;
}
