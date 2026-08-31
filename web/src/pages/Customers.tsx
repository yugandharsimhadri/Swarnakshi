import PartyMaster, { type PartyMasterConfig } from "@/components/PartyMaster";

const config: PartyMasterConfig = {
  resource: "customers",
  title: "Customers",
  subtitle: "Customer master",
  addLabel: "+ Add Customer",
  searchPlaceholder: "Search customers…",
  singular: "customer",
  hasCompany: false,
  hasType: false,
  hasBankDetails: false,
  deactivateBody:
    "This customer will no longer be available for new projects or new customer-related " +
    "transactions. Existing historical records will remain unchanged.",
  reactivateBody:
    "This customer becomes available again for new projects and customer payments. " +
    "Historical records are unchanged.",
};

export default function Customers() {
  return <PartyMaster config={config} />;
}
