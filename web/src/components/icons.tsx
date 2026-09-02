import type { SVGProps } from "react";

/**
 * Line icons, drawn to match the Blueprint palette: constant-weight strokes on a 24-unit grid,
 * the way a detail is drawn on a drawing sheet. Everything inherits `currentColor`, so an icon in
 * an active tab, a muted list row and a coloured chip are the same component.
 *
 * Hand-drawn rather than pulled from an icon package on purpose. The set is small, half of it is
 * construction-specific (hard hat, cement bag, tipper, crane) and no library ships those — the
 * generic substitutes are exactly what makes business software look like it was assembled rather
 * than designed.
 */
type IconProps = SVGProps<SVGSVGElement> & { size?: number };

function Icon({ size = 22, children, ...rest }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...rest}
    >
      {children}
    </svg>
  );
}

// ---- bottom bar -----------------------------------------------------------

/** Home: a pitched roof over a plan, not a generic house — this is a builder's dashboard. */
export const IconHome = (p: IconProps) => (
  <Icon {...p}>
    <path d="M3.5 10.6 12 4l8.5 6.6" />
    <path d="M5.6 9v10.5h12.8V9" />
    <path d="M9.7 19.5v-5.2h4.6v5.2" />
  </Icon>
);

/** Projects: villas in a row, which is what a project is here. */
export const IconProjects = (p: IconProps) => (
  <Icon {...p}>
    <path d="M3 20h18" />
    <path d="M4.5 20V9.5L9 6.5l4.5 3V20" />
    <path d="M13.5 12.5 18 10v10" />
    <path d="M7.3 12.2h3.4M7.3 15.6h3.4M16 13.5h1.2M16 16.5h1.2" />
  </Icon>
);

/** Inventory: stacked stock on a pallet. */
export const IconInventory = (p: IconProps) => (
  <Icon {...p}>
    <path d="M3.4 20.2h17.2" />
    <rect x="4.4" y="11" width="6.6" height="6.4" rx="0.9" />
    <rect x="13" y="11" width="6.6" height="6.4" rx="0.9" />
    <rect x="8.7" y="4" width="6.6" height="6.4" rx="0.9" />
    <path d="M11.2 4v2.6M6.9 11v2.4M15.5 11v2.4" />
  </Icon>
);

/**
 * Approvals: a checked-off clipboard. A rubber stamp is the truer metaphor for what an owner
 * does, but at 21px in a tab bar it reads as a trophy — and a tab label has one chance.
 */
export const IconApprovals = (p: IconProps) => (
  <Icon {...p}>
    <path d="M9.4 3.4h5.2a1 1 0 0 1 1 1v1.4H8.4V4.4a1 1 0 0 1 1-1z" />
    <path d="M15.6 5.8h2a1.4 1.4 0 0 1 1.4 1.4v12.4a1.4 1.4 0 0 1-1.4 1.4H6.4A1.4 1.4 0 0 1 5 19.6V7.2a1.4 1.4 0 0 1 1.4-1.4h2" />
    <path d="m8.8 13.4 2.3 2.3 4.3-4.6" />
  </Icon>
);

/** More: a stack of sheets, i.e. everything else on the drawing table. */
export const IconMore = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4 7h16M4 12h16M4 17h10" />
  </Icon>
);

// ---- masters --------------------------------------------------------------

/** Sites: a tower crane. Nothing says "a place where building happens" faster. */
export const IconSite = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4.5 20.5h7" />
    <path d="M8 20.5V6" />
    <path d="M3 6h17" />
    <path d="M8 6 5.2 9.4M8 6l2.8 3.4" />
    <path d="M17 6v4.2" />
    <path d="M15.6 10.2h2.8l-1.4 2.3z" />
  </Icon>
);

/** Materials: a cement sack — folded neck, bulging body. The unit of construction stock. */
export const IconMaterials = (p: IconProps) => (
  <Icon {...p}>
    <path d="M9.8 3.4h4.4l1 2.7H8.8z" />
    <path d="M8.8 6.1C6.9 7.5 5.8 9.6 5.8 12v7a1.9 1.9 0 0 0 1.9 1.9h8.6a1.9 1.9 0 0 0 1.9-1.9v-7c0-2.4-1.1-4.5-3-5.9" />
    <path d="M9.4 12.8h5.2M9.4 16h5.2" />
  </Icon>
);

/** Contractors: a hard hat — wide brim, banded crown. */
export const IconContractor = (p: IconProps) => (
  <Icon {...p}>
    <path d="M2.6 17h18.8" />
    <path d="M5 17v-1.8a7 7 0 0 1 14 0V17" />
    <path d="M8.5 10.5a9.4 9.4 0 0 1 7 0" />
  </Icon>
);

/** Customers: one named person you sell a villa to. */
export const IconCustomer = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="12" cy="8.4" r="3.6" />
    <path d="M5.2 20.2a6.8 6.8 0 0 1 13.6 0" />
  </Icon>
);

/** Employees: your crew — more than one, on your payroll. */
export const IconEmployees = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="9.4" cy="8.6" r="3.3" />
    <path d="M3.4 19.6a6 6 0 0 1 12 0" />
    <path d="M16 6.1a3.3 3.3 0 0 1 0 6.4" />
    <path d="M17.2 14.6a6 6 0 0 1 3.4 5" />
  </Icon>
);

/** Users & access: a key. */
export const IconAccess = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="8" cy="8" r="3.6" />
    <path d="M10.6 10.6 20 20" />
    <path d="M17.2 17.2 15.4 19M19.4 15 17.6 16.8" />
  </Icon>
);

/** Reports: bars on a rule. */
export const IconReports = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4 20h16" />
    <path d="M7 20v-5.4M11.7 20V8.6M16.4 20v-8" />
  </Icon>
);

// ---- movement -------------------------------------------------------------

/** Purchases: a supplier's invoice. */
export const IconPurchase = (p: IconProps) => (
  <Icon {...p}>
    <path d="M6 3.6h12v17.2l-2.4-1.5-2.4 1.5-2.4-1.5-2.4 1.5L6 20.8z" />
    <path d="M9.2 8.6h5.6M9.2 12.2h5.6M9.2 15.6h3.4" />
  </Icon>
);

/** Requests: stock moving out of the store and into a villa. */
export const IconRequest = (p: IconProps) => (
  <Icon {...p}>
    <path d="M3.6 8.6h13.2M13.4 5.2l3.4 3.4-3.4 3.4" />
    <path d="M20.4 15.4H7.2M10.6 12l-3.4 3.4L10.6 18.8" />
  </Icon>
);

/** Take from store: a crate opening outward. */
export const IconIssue = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4 9.4v9.4a1.2 1.2 0 0 0 1.2 1.2h13.6a1.2 1.2 0 0 0 1.2-1.2V9.4" />
    <path d="M3.2 5.6h17.6v3.8H3.2z" />
    <path d="M12 19.4v-6.6M12 12.8 9.8 15M12 12.8 14.2 15" />
  </Icon>
);

/** Bought for this villa: a tipper arriving at site. */
export const IconDelivery = (p: IconProps) => (
  <Icon {...p}>
    <path d="M2.6 16.4V7.2h10.6v9.2" />
    <path d="M13.2 10.2h3.6l3.6 3.4v2.8h-1.4" />
    <path d="M8.6 16.4h3.2" />
    <circle cx="6.6" cy="17.8" r="1.8" />
    <circle cx="17.2" cy="17.8" r="1.8" />
  </Icon>
);

/** Expenses: notes changing hands. */
export const IconExpense = (p: IconProps) => (
  <Icon {...p}>
    <rect x="2.8" y="6.6" width="18.4" height="10.8" rx="1.4" />
    <circle cx="12" cy="12" r="2.6" />
    <path d="M6.2 10.2v3.6M17.8 10.2v3.6" />
  </Icon>
);

// ---- controls -------------------------------------------------------------

export const IconBack = (p: IconProps) => (
  <Icon {...p}>
    <path d="M19 12H5.6" />
    <path d="M11.4 5.6 5 12l6.4 6.4" />
  </Icon>
);

export const IconChevron = (p: IconProps) => (
  <Icon {...p}>
    <path d="M9.4 5.6 15.8 12l-6.4 6.4" />
  </Icon>
);

export const IconSun = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="12" cy="12" r="4" />
    <path d="M12 2.8v2.4M12 18.8v2.4M4.7 4.7l1.7 1.7M17.6 17.6l1.7 1.7M2.8 12h2.4M18.8 12h2.4M4.7 19.3l1.7-1.7M17.6 6.4l1.7-1.7" />
  </Icon>
);

export const IconMoon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M20 14.4A8.4 8.4 0 0 1 9.6 4a8.4 8.4 0 1 0 10.4 10.4z" />
  </Icon>
);

/**
 * The mark. A villa's gable held inside a surveyor's crosshair — the two halves of the app,
 * the thing being built and the act of checking it.
 */
export const Logomark = ({ size = 44, ...rest }: IconProps) => (
  <svg width={size} height={size} viewBox="0 0 48 48" fill="none" aria-hidden="true" {...rest}>
    <circle cx="24" cy="24" r="20.5" stroke="currentColor" strokeWidth="1.5" opacity="0.35" />
    <path d="M24 1.5v7M24 39.5v7M1.5 24h7M39.5 24h7"
      stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" opacity="0.35" />
    <path d="M11.5 26.5 24 15l12.5 11.5"
      stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" />
    <path d="M15.5 24.2V33h17v-8.8"
      stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" />
    <circle cx="24" cy="24" r="2.1" fill="currentColor" />
  </svg>
);
