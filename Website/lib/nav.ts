export interface NavItem {
  label: string;
  href: string;
}

export const NAV_ITEMS: NavItem[] = [
  { label: "Home", href: "/" },
  { label: "About", href: "/about" },
  { label: "Services", href: "/services" },
  { label: "Fleet", href: "/fleet" },
  { label: "Booking", href: "/booking" },
  { label: "FAQ", href: "/faq" },
  { label: "Contact", href: "/contact" },
];
