import PartyMaster, { type PartyMasterConfig } from "@/components/PartyMaster";

const config: PartyMasterConfig = {
  resource: "contractors",
  title: "Contractors",
  subtitle: "Contractor master",
  addLabel: "+ Add Contractor",
  searchPlaceholder: "Search contractors…",
  singular: "contractor",
  hasCompany: true,
  hasType: true,
  hasBankDetails: true,
  deactivateBody:
    "This contractor will no longer be available for new contracts or new contractor-related " +
    "transactions. Existing historical records will remain unchanged.",
  reactivateBody:
    "This contractor becomes available again for new contracts and contractor payments. " +
    "Historical records are unchanged.",
};

export default function Contractors() {
  return <PartyMaster config={config} />;
}
