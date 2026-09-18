import type { InputHTMLAttributes } from "react";

export interface TextInputProps extends InputHTMLAttributes<HTMLInputElement> {
  /**
   * Visible label. The wireframes show placeholder-only fields; the label is rendered
   * for assistive technology so the field is not left unlabelled.
   */
  label: string;
  hideLabel?: boolean;
  error?: string;
  hint?: string;
}

/**
 * The shared field styling.
 *
 * Exported so the many one-off `<input>`, `<select>` and `<textarea>` elements across
 * the app can look the same without each restating a dozen utility classes — which is
 * how a form ends up with four subtly different input heights.
 */
export const fieldClassName =
  "w-full rounded-field border border-border bg-surface px-3.5 font-control text-sm "
  + "text-text-primary placeholder:text-text-placeholder "
  + "transition-[border-color,box-shadow] duration-200 "
  + "hover:border-border-strong "
  + "focus:border-brand-primary focus:outline-none focus:ring-4 focus:ring-brand-primary/10 "
  + "disabled:cursor-not-allowed disabled:bg-surface-muted disabled:text-text-muted";

/** Height matched to the medium button, so a field and a button sit level. */
export const inputClassName = `h-11 ${fieldClassName}`;

export const textareaClassName = `py-3 ${fieldClassName}`;

export function TextInput({
  label,
  hideLabel = false,
  error,
  hint,
  id,
  className = "",
  ...props
}: TextInputProps) {
  const inputId = id ?? props.name;
  const errorId = error ? `${inputId}-error` : undefined;
  const hintId = hint ? `${inputId}-hint` : undefined;

  return (
    <div className="w-full">
      <label
        htmlFor={inputId}
        className={
          hideLabel
            ? "sr-only"
            : "mb-1.5 block font-control text-sm font-medium text-text-secondary"
        }
      >
        {label}
      </label>

      <input
        id={inputId}
        aria-invalid={error ? true : undefined}
        aria-describedby={[errorId, hintId].filter(Boolean).join(" ") || undefined}
        className={`${inputClassName} ${
          error ? "border-danger-accent focus:border-danger-accent focus:ring-danger-accent/10" : ""
        } ${className}`}
        {...props}
      />

      {hint && !error ? (
        <p id={hintId} className="mt-1.5 text-sm text-text-secondary">
          {hint}
        </p>
      ) : null}

      {error ? (
        <p id={errorId} className="mt-1.5 font-control text-sm text-danger-accent">
          {error}
        </p>
      ) : null}
    </div>
  );
}
