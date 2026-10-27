import type { ButtonHTMLAttributes } from "react";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "subtle" | "danger" | "light";
export type ButtonSize = "sm" | "md" | "lg" | "xl";

/*
 * Buttons.
 *
 * Five variants because five distinct jobs turned up across the app, not because a
 * palette wanted filling: the one thing to do, the warm alternative, an outlined
 * option, a quiet in-place action, and something destructive.
 *
 * The active-state scale is deliberate. A button that moves under the finger is the
 * cheapest way to make a web app feel like it responded, and it costs one transform.
 */
const variantClasses: Record<ButtonVariant, string> = {
  primary:
    "bg-brand-primary text-text-on-brand shadow-brand hover:bg-brand-primary-hover hover:-translate-y-px active:translate-y-0",
  secondary:
    "bg-brand-secondary text-white shadow-sm hover:bg-brand-secondary-hover hover:-translate-y-px active:translate-y-0",
  ghost:
    "border border-border-strong bg-surface text-text-primary shadow-xs hover:border-brand-primary hover:text-brand-primary",
  subtle:
    "bg-surface-muted text-text-primary hover:bg-border",
  // For full-bleed deep-teal panels, where the other variants would sit on their own colour.
  light:
    "bg-white text-brand-deep shadow-md hover:bg-brand-soft hover:-translate-y-px active:translate-y-0",
  danger:
    "bg-danger-soft text-danger-accent hover:bg-danger-accent hover:text-white",
};

const sizeClasses: Record<ButtonSize, string> = {
  sm: "h-9 px-3.5 text-sm gap-1.5",
  md: "h-11 px-5 text-sm gap-2",
  lg: "h-12 px-6 text-base gap-2",
  xl: "h-14 px-8 text-base gap-2.5",
};

const baseClasses =
  "inline-flex items-center justify-center rounded-control font-control font-semibold "
  + "transition-all duration-200 ease-[cubic-bezier(0.22,1,0.36,1)] "
  + "active:scale-[0.98] "
  + "disabled:pointer-events-none disabled:opacity-50 disabled:shadow-none";

/**
 * Button styling as a class string, for links that should look like buttons.
 * A link that navigates must stay an anchor for keyboard and screen-reader users.
 */
export function buttonClassName(
  variant: ButtonVariant = "primary",
  className = "",
  size: ButtonSize = "md",
) {
  return `${baseClasses} ${sizeClasses[size]} ${variantClasses[variant]} ${className}`;
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
}

export function Button({
  variant = "primary",
  size = "md",
  className = "",
  ...props
}: ButtonProps) {
  return <button className={buttonClassName(variant, className, size)} {...props} />;
}
