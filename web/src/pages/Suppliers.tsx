import PartyMaster, { type PartyMasterConfig } from "@/components/PartyMaster";

/**
 * Suppliers are usually created by *typing a name* on a purchase — see NewPurchase. This screen is
 * for filling in the rest afterwards: GSTIN, bank details, the office address.
 */
const config: PartyMasterConfig = {
  resource: "suppliers",
  title: "Suppliers",
  subtitle: "Supplier master",
  addLabel: "+ Add Supplier",
  searchPlaceholder: "Search suppliers…",
  singular: "supplier",
  hasCompany: true,
  hasType: false,
  hasBankDetails: true,
  deactivateBody:
    "This supplier will no longer appear when recording a purchase. Existing purchases and their " +
    "payment history are unchanged.",
  reactivateBody:
    "This supplier becomes available again for new purchases. Historical records are unchanged.",
};

export default function Suppliers() {
  return <PartyMaster config={config} />;
}
